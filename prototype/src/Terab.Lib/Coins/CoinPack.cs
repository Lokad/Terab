// Copyright Lokad 2018 under MIT BCH.
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Terab.Lib.Chains;
using Terab.Lib.Messaging;

namespace Terab.Lib.Coins
{
    /// <summary>
    /// Packing coins in a way to allow zero-copy read access to them
    /// while loading them from a sector (i.e. data storage block).
    /// </summary>
    /// <remarks>
    /// Serialization format:
    /// - Header
    /// - list of OutpointSig
    /// - list of coins
    /// </remarks>
    public unsafe ref struct CoinPack
    {
        public const byte CurrentVersion = 1;

        /// <remarks> The buffer can exceed the content of the coin pack. </remarks>
        private Span<byte> _buffer;

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct Header
        {
            public readonly static int SizeInBytes = sizeof(Header);

            public uint SectorIndex;
            public byte LayerIndex;
            public byte Version;
            public byte WriteColor;
            public byte WriteCount;
            public int PackSizeInBytes;
            public ushort OutpointSigCount;
            public ushort OutpointCount;
            public bool OutpointSigsOverflow;
        }

        private int FirstCoinOffset => Header.SizeInBytes + OutpointSigCount * OutpointSig.SizeInBytes;

        private ref Header AsHeader
        {
            get => ref MemoryMarshal.Cast<byte, Header>(_buffer)[0];
        }

        public uint SectorIndex
        {
            get => AsHeader.SectorIndex;
            set => AsHeader.SectorIndex = value;
        }

        public byte LayerIndex
        {
            get => AsHeader.LayerIndex;
            set => AsHeader.LayerIndex = value;
        }

        /// <summary> Zero indicates lack of initialization. Expected current version is '1'. </summary>
        public byte Version
        {
            get => AsHeader.Version;
            set => AsHeader.Version = value;
        }

        public byte WriteColor
        {
            get => AsHeader.WriteColor;
            set => AsHeader.WriteColor = value;
        }

        public byte WriteCount
        {
            get => AsHeader.WriteCount;
            set => AsHeader.WriteCount = value;
        }

        /// <summary>
        /// Return header size plus pack content size in bytes.
        /// </summary>
        public int SizeInBytes
        {
            get => AsHeader.PackSizeInBytes + Header.SizeInBytes;
            set => AsHeader.PackSizeInBytes = value - Header.SizeInBytes;
        }

        public ushort OutpointSigCount
        {
            get => AsHeader.OutpointSigCount;
            set => AsHeader.OutpointSigCount = value;
        }

        public ushort CoinCount
        {
            get => AsHeader.OutpointCount;
            set => AsHeader.OutpointCount = value;
        }

        public bool IsOutpointSigWritten => AsHeader.OutpointSigCount > 0 || AsHeader.OutpointCount > 0;

        public Span<byte> Span => _buffer.Slice(0, SizeInBytes);

        public bool IsEmpty => Span.Length == 0;

        public static CoinPack Empty => new CoinPack(Span<byte>.Empty);

        public CoinPack(Span<byte> buffer)
        {
            _buffer = buffer;
        }

        public static CoinPack WithExtraCoin(CoinPack original, Coin toAppend, SpanPool<byte> pool)
        {
            var newPack = new CoinPack(pool.GetSpan(original.SizeInBytes + toAppend.SizeInBytes));

            original.Span.CopyTo(newPack._buffer);
            newPack.Append(toAppend);

            return newPack;
        }

        public static CoinPack WithExtraCoins(CoinPack original, CoinPack toAppend, int coinOffset, int coinCount,
            SpanPool<byte> pool)
        {
            var newPack = new CoinPack(pool.GetSpan(original.SizeInBytes + toAppend.SizeInBytes));
            original.Span.CopyTo(newPack._buffer);
            newPack.Append(toAppend, coinOffset, coinCount);

            return newPack;
        }

        public static CoinPack WithExtraCoinEvent(CoinPack original, Coin toUpdate, int coinOffset, CoinEvent evt, SpanPool<byte> pool)
        {
            var newCoin = Coin.WithExtraEvent(toUpdate, evt, pool);

            return WithChangedCoinEvent(original, newCoin, coinOffset, pool);
        }

        public static CoinPack WithLessCoinEvent(CoinPack original, Coin newCoin, int coinOffset, SpanPool<byte> pool)
        {
            return WithChangedCoinEvent(original, newCoin, coinOffset, pool);
        }

        private static CoinPack WithChangedCoinEvent(CoinPack original, Coin newCoin, int coinOffset, SpanPool<byte> pool)
        {
            var newPack = new CoinPack(pool.GetSpan(original.SizeInBytes + CoinEvent.SizeInBytes));

            original.Span.Slice(0, original.FirstCoinOffset).CopyTo(newPack._buffer);

            // below two statements clear the "coin part" of newPack, inherited from original pack.
            newPack.SizeInBytes = original.FirstCoinOffset;
            newPack.CoinCount = 0;

            newPack.Append(original, 0, coinOffset);
            newPack.Append(newCoin);
            newPack.Append(original, coinOffset + 1, original.CoinCount - coinOffset - 1);

            return newPack;
        }

        public static CoinPack WithExtraOutpointSig(CoinPack original, OutpointSig toAdd, SpanPool<byte> pool)
        {
            var newPack = new CoinPack(pool.GetSpan(original.SizeInBytes + OutpointSig.SizeInBytes));

            original.Span.Slice(0, Header.SizeInBytes).CopyTo(newPack._buffer);

            if (original.OutpointSigCount < Constants.CoinStoreOutpointSigMaxCount)
            {
                // Regular insertion of the extra 'OutpointSig'.
                newPack.OutpointSigCount = (ushort) (original.OutpointSigCount + 1);

                var sigs = MemoryMarshal.Cast<byte, OutpointSig>(
                    newPack._buffer.Slice(Header.SizeInBytes,
                        (original.OutpointSigCount + 1) * OutpointSig.SizeInBytes));

                // we should have exactly one more sig
                Debug.Assert(sigs.Length == original.OutpointSigs.Length + 1);

                // insert at the beginning on purpose, most recent will be tentatively matched first
                sigs[0] = toAdd;
                original.OutpointSigs.CopyTo(sigs.Slice(1));
            }
            else
            {
                // 'OutpointSigs' are overflowing.
                newPack.OutpointSigCount = 0;
                newPack.AsHeader.OutpointSigsOverflow = true;
            }

            original.Span.Slice(original.FirstCoinOffset).CopyTo(newPack._buffer.Slice(newPack.FirstCoinOffset));

            return newPack;
        }

        public static CoinPack WithPruning(CoinPack original, ILineage lineage, IOutpointHash hash, SpanPool<byte> pool,
                                           out Span<OutpointSig> prunedSigs)
        {
            var newPack = new CoinPack(pool.GetSpan(original.SizeInBytes));

            original.Span.Slice(0, original.FirstCoinOffset).CopyTo(newPack._buffer);

            // below two statements clear the "coin part" of newPack, inherited from original pack.
            newPack.SizeInBytes = original.FirstCoinOffset;
            newPack.CoinCount = 0;

            newPack.PruneAndAppend(original, lineage, hash, pool, out prunedSigs);

            return newPack;
        }

        /// <summary>
        /// Coin pack with coins from 'original' minus skipping the initial segment of coins.
        /// The skipped coins are replaced by their outpoint signatures.
        /// </summary>
        public static CoinPack WithSkippedCoins(CoinPack original, int coinSkipCount, IOutpointHash hash, SpanPool<byte> pool)
        {
            var newPack = new CoinPack(pool.GetSpan(original.SizeInBytes));

            if (original.LayerIndex == 0)
            {
                var extraSigs = original.ExtractSigs(coinSkipCount, hash, pool);

                original._buffer.Slice(0, Header.SizeInBytes).CopyTo(newPack._buffer);

                var sigs = MemoryMarshal.Cast<byte, OutpointSig>(
                    newPack._buffer.Slice(Header.SizeInBytes, (original.OutpointSigCount + coinSkipCount) * OutpointSig.SizeInBytes));

                extraSigs.CopyTo(sigs);
                original.OutpointSigs.CopyTo(sigs.Slice(coinSkipCount));
                newPack.OutpointSigCount = (ushort) (original.OutpointSigCount + coinSkipCount);
            }
            else
            {
                original._buffer.Slice(0, original.FirstCoinOffset).CopyTo(newPack._buffer);
            }

            // below two statements clear the "coin part" of newPack, inherited from original pack.
            newPack.SizeInBytes = newPack.FirstCoinOffset;
            newPack.CoinCount = 0;

            newPack.Append(original, coinSkipCount, original.CoinCount - coinSkipCount);

            return newPack;
        }

        public static CoinPack WithLessOutpointSigs(CoinPack original, Span<OutpointSig> sigs, SpanPool<byte> pool)
        {
            var newPack = new CoinPack(pool.GetSpan(original.SizeInBytes));
            Span<OutpointSig> sigTempBuffer = stackalloc OutpointSig[original.OutpointSigCount];

            // at the end of outer foreach, sigTempBuffer would contain those from original CoinPack, not present in 
            // sigs
            var sigCount = 0;
            foreach (var sig in original.OutpointSigs)
            {
                var found = false;
                foreach (var pruned in sigs)
                {
                    if (pruned.Equals(sig))
                    {
                        found = true;
                        break;
                    }
                }

                if (!found)
                    sigTempBuffer[sigCount++] = sig;
            }

            sigTempBuffer.Slice(0, sigCount).CopyTo(MemoryMarshal.Cast<byte, OutpointSig>(
                newPack._buffer.Slice(Header.SizeInBytes, sigCount * OutpointSig.SizeInBytes)));

            newPack.OutpointSigCount = (ushort) sigCount;
            newPack.SizeInBytes = newPack.FirstCoinOffset;
            newPack.CoinCount = 0;
            newPack.Append(original, 0, original.CoinCount);

            return newPack;
        }


        public void Reset()
        {
            _buffer.Slice(0, Header.SizeInBytes).Clear();
        }

        /// <summary> Signatures of outpoints that *below* the present coin pack within the Sozu table. </summary>
        public Span<OutpointSig> OutpointSigs => 
            MemoryMarshal.Cast<byte, OutpointSig>(
                _buffer.Slice(Header.SizeInBytes, OutpointSigCount * OutpointSig.SizeInBytes));

        /// <summary> Must be called before any call to 'Append'. </summary>
        [Obsolete("TODO: Replace call with 'WithExtraOutpointSig'.")]
        public void SetOutpointSigs(ReadOnlySpan<OutpointSig> sigs)
        {
            if (sigs.Length >= ushort.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(sigs));

            if (IsOutpointSigWritten)
                throw new InvalidOperationException();

            OutpointSigCount = (ushort)sigs.Length;

            var offset = SizeInBytes;
            var destination = MemoryMarshal.Cast<byte, OutpointSig>(_buffer.Slice(offset));
            sigs.CopyTo(destination);

            SizeInBytes = offset + sigs.Length * OutpointSig.SizeInBytes;
        }

        public void Append(Coin coin)
        {
            var offset = SizeInBytes;

            var destination = _buffer.Slice(offset);
            var coinSpan = coin.Span;

            coinSpan.CopyTo(destination);
            SizeInBytes = offset + coinSpan.Length;
            CoinCount += 1;
        }

        /// <summary>
        /// Appends a segment of coins, treating 'from' as a list of coins.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="coinOffset">Index of the first coin to be appended.</param>
        /// <param name="coinCount"> Count of coins to be appended.</param>
        public void Append(CoinPack source, int coinOffset, int coinCount)
        {
            if(coinOffset < 0 || coinOffset > source.CoinCount)
                throw new ArgumentOutOfRangeException(nameof(coinOffset));

            if (coinCount == 0)
                return; // nothing to do.

            if(coinCount < 0 || coinOffset + coinCount > source.CoinCount)
                throw new ArgumentOutOfRangeException(nameof(coinCount));

            var span = source._buffer.Slice(source.FirstCoinOffset);

            // skip Coins
            for (var i = 0; i < coinOffset; i++)
            {
                var coin = new Coin(span);
                span = span.Slice(coin.SizeInBytes);
            }

            // append Coins
            for (var i = 0; i < coinCount; i++)
            {
                var coin = new Coin(span);
                Append(coin);

                if (i < coinCount - 1)
                    span = span.Slice(coin.SizeInBytes);
            }
        }

        /// <summary>
        /// Append all the coins from the source unless they can be pruned.
        /// </summary>
        public void PruneAndAppend(CoinPack source, ILineage lineage, IOutpointHash hash,
                                   SpanPool<byte> spanPool, out Span<OutpointSig> prunedSigs)
        {
            var span = source._buffer.Slice(source.FirstCoinOffset);
            prunedSigs = MemoryMarshal.Cast<byte, OutpointSig>(spanPool.GetSpan(source.CoinCount * OutpointSig.SizeInBytes));

            var coinCount = source.CoinCount;
            var prunedCount = 0;
            for (var i = 0; i < coinCount; i++)
            {
                var coin = new Coin(span);

                if (!lineage.IsCoinPrunable(coin.Events))
                {
                    Append(coin);
                }
                else
                {
                    prunedSigs[prunedCount++] = OutpointSig.From(hash.Hash(ref coin.Outpoint));
                }

                span = span.Slice(coin.SizeInBytes);
            }

            prunedSigs = prunedSigs.Slice(0, prunedCount);
        }

        /// <summary>
        /// Returns 'true' when the outpoint is found, and populate 'coin'; returns 'false' otherwise.
        /// </summary>
        public bool TryGet(ref Outpoint outpoint, out Coin coin)
        {
            return TryGet(ref outpoint, out coin, out var coinOffset);
        }

        public bool TryGet(ref Outpoint outpoint, out Coin coin, out int coinOffset)
        {
            var span = _buffer.Slice(FirstCoinOffset);
            var first = outpoint.TxId[0];

            var coinCount = CoinCount;
            for (var i = 0; i < coinCount; i++)
            {
                coin = new Coin(span);
                var txoFirst = coin.Outpoint.TxId[0];
                if (first == txoFirst)
                {
                    if (outpoint.Equals(coin.Outpoint))
                    {
                        coinOffset = i;
                        return true;
                    }
                }

                if (i < coinCount - 1)
                    span = span.Slice(coin.SizeInBytes);
            }

            coin = Coin.Empty;
            coinOffset = -1;
            return false;
        }

        public int CountCoinsAbove(int thresholdInBytes)
        { 
            if(thresholdInBytes < 0)
                throw new ArgumentOutOfRangeException(nameof(thresholdInBytes));

            var span = _buffer.Slice(FirstCoinOffset);

            var totalLength = 0;
            var coinCount = CoinCount;
            for (var i = 0; i < coinCount; i++)
            {
                var coin = new Coin(span);
                totalLength += coin.SizeInBytes;

                if (totalLength >= thresholdInBytes)
                    return i + 1; // +1 because it's a count

                if (i < coinCount - 1)
                    span = span.Slice(coin.SizeInBytes);
            }

            return coinCount;
        }

        public Span<OutpointSig> ExtractSigs(int coinCount, IOutpointHash hash, SpanPool<byte> pool)
        {
            var sigs = MemoryMarshal.Cast<byte, OutpointSig>(pool.GetSpan(coinCount * OutpointSig.SizeInBytes));

            var span = _buffer.Slice(FirstCoinOffset);

            for (var i = 0; i < coinCount; i++)
            {
                var coin = new Coin(span);
                sigs[i] = OutpointSig.From(hash.Hash(ref coin.Outpoint));

                if (i < coinCount - 1)
                    span = span.Slice(coin.SizeInBytes);
            }

            return sigs;
        }

        public bool PositiveMatch(OutpointSig candidate)
        {
            // When filter is overflowing, then treats all matches as positive.
            if (AsHeader.OutpointSigsOverflow)
                return true;

            // PERF: [vermorel] #118 performance can be improved with SIMD
            // See https://stackoverflow.com/questions/53342460/fatest-way-to-find-if-a-ushort-is-present-within-a-spanushort-with-simd 

            foreach (var sig in OutpointSigs)
                if (sig.Equals(candidate))
                    return true;

            return false;
        }
    }
}
