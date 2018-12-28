// Copyright Lokad 2018 under MIT BCH.
using System.Diagnostics;
using Terab.Lib.Chains;
using Terab.Lib.Coins.Exceptions;
using Terab.Lib.Messaging;

namespace Terab.Lib.Coins
{
    /// <summary> Layered persistent hash table, with a regular key-value store
    /// as last layer. </summary>
    public class SozuTable : ICoinStore
    {
        /// <summary> Intended as a threshold for direct storage in the key-value store.</summary>
        /// <remarks> The goal is to avoid overflowing the first layer with tiny few extra large coins. </remarks>
        public const int PayloadOversizeInBytes = 512;

        /// <summary> When overflowing, how many coins (expressed in bytes) should flow down
        /// from one layer to another. </summary>
        const int OverflowChunkInBytes = 1024;

        /// <summary> Size in bytes of the 'SpanPool' used to avoid allocations within
        /// the I/O operations performed by the Sozu table. </summary>
        const int SpanPoolCapacity = 256 * 4096;

        private readonly IPackStore _store;

        /// <summary> Re-hashing is needed on overflow. </summary>
        private readonly IOutpointHash _hash;

        private readonly SpanPool<byte> _pool;

        public SozuTable(IPackStore store, IOutpointHash hash)
        {
            _store = store;
            _hash = hash;
            _pool = new SpanPool<byte>(SpanPoolCapacity);
        }

        public bool TryGet(ulong outpointHash, ref Outpoint outpoint, BlockAlias context, ILineage lineage,
            out Coin coin, out BlockAlias production, out BlockAlias consumption)
        {
            var sectorIndex = (uint)(outpointHash % (uint) _store.SectorCount);
            production = BlockAlias.Undefined;
            consumption = BlockAlias.Undefined;

            // Check layer 0.
            var pack0 = _store.Read(layerIndex: 0, sectorIndex);
            if (pack0.TryGet(ref outpoint, out coin))
            {
                return lineage.TryGetEventsInContext(coin.Events, context, out production, out consumption);
            }

            var sig = OutpointSig.From(outpointHash);

            // Layer 0 has a probabilistic filter. No false negative are possible.
            if (!pack0.PositiveMatch(sig))
                return false;

            // Check next layers.
            for (var layerIndex = 1; layerIndex < _store.LayerCount; layerIndex++)
            {
                var packN = _store.Read(layerIndex: layerIndex, sectorIndex);

                if (packN.TryGet(ref outpoint, out coin))
                {
                    return lineage.TryGetEventsInContext(coin.Events, context, out production, out consumption);
                }
            }

            // Not found, it was a false positive on the probabilistic filter.
            return false;
        }
        public CoinChangeStatus AddProduction(
            ulong outpointHash, 
            ref Outpoint outpoint, 
            bool isCoinBase, 
            Payload payload, 
            BlockAlias context, 
            ILineage lineage)
        {
            try
            {
                var sectorIndex = (uint) (outpointHash % (uint) _store.SectorCount);
                var sig = OutpointSig.From(outpointHash);
                var evt = new CoinEvent(context, CoinEventKind.Production);
               
                // Oversized payload, insert in the last layer directly.
                if (payload.SizeInBytes > PayloadOversizeInBytes)
                {
                    // HACK: [vermorel] edge-case, is-it possible to have an oversized coinbase?
                    return AddOversizedProduction(sectorIndex, sig, ref outpoint, isCoinBase, payload, evt, lineage);
                }

                // Layer 0, the only layer to have outpoint signatures.
                var pack0 = _store.Read(layerIndex: 0, sectorIndex);

                var extendedPack = CoinPack.Empty;

                // The coin already exists on another chain.
                if (pack0.TryGet(ref outpoint, out var coin, out var coinOffset))
                {
                    if (coin.HasEvent(evt))
                        return CoinChangeStatus.Success;

                    if (!lineage.IsAddConsistent(coin.Events, evt))
                        return CoinChangeStatus.InvalidContext;

                    // HACK: [vermorel] edge-case, is-it possible to have the same coinbase on multiple chains?
                    extendedPack = CoinPack.WithExtraCoinEvent(pack0, coin, coinOffset, evt, _pool);

                }
                // A probabilistic match exists, checking deeper layers.
                else if (pack0.PositiveMatch(sig))
                {
                    for (var layerIndex = 1; layerIndex < _store.LayerCount; layerIndex++)
                    {
                        // Check deeper layers
                        var packN = _store.Read(layerIndex: layerIndex, sectorIndex);
                        if (packN.TryGet(ref outpoint, out coin, out coinOffset))
                        {
                            if (coin.HasEvent(evt))
                                return CoinChangeStatus.Success;

                            if (!lineage.IsAddConsistent(coin.Events, evt))
                                return CoinChangeStatus.InvalidContext;

                            extendedPack = CoinPack.WithExtraCoinEvent(packN, coin, coinOffset, evt, _pool);
                            break;
                        }

                        // False-positive match, adding the coin as new in layer 0.
                        if(layerIndex == _store.LayerCount - 1)
                        {
                            var newCoin = new Coin(ref outpoint, isCoinBase, payload, evt, _pool);
                            extendedPack = CoinPack.WithExtraCoin(pack0, newCoin, _pool);
                        }
                    }

                    // 'for' loop above should not exit without assigned a value to 'extendedPack'.
                    Debug.Assert(!extendedPack.IsEmpty);
                }
                else // The coin does not exist on any chain.
                {
                    var newCoin = new Coin(ref outpoint, isCoinBase, payload, evt, _pool);
                    extendedPack = CoinPack.WithExtraCoin(pack0, newCoin, _pool);
                }

                // Check of overflow
                var packs = extendedPack.SizeInBytes < _store.SectorSizeInBytes[extendedPack.LayerIndex] 
                    ? new ShortCoinPackCollection(extendedPack) 
                    : GetOverflow(extendedPack, lineage, _hash);

                // Persist the pack(s), several coin packs in case of overflow.
                _store.Write(packs);

                return CoinChangeStatus.Success;
            }
            finally
            {
                _pool.Reset();
            }
        }

        /// <summary>
        /// Oversized coins go straight to the final layer.
        /// </summary>
        private CoinChangeStatus AddOversizedProduction(
            uint sectorIndex,
            OutpointSig sig,
            ref Outpoint outpoint,
            bool isCoinBase,
            Payload payload,
            CoinEvent evt,
            ILineage lineage)
        {
            // Jump to last layer
            var pack = _store.Read(_store.LayerCount - 1, sectorIndex);

            // The coin already exists on another chain.
            if (pack.TryGet(ref outpoint, out Coin coin, out int coinOffset))
            {
                if (coin.HasEvent(evt))
                    return CoinChangeStatus.Success;

                if (!lineage.IsAddConsistent(coin.Events, evt))
                    return CoinChangeStatus.InvalidContext;

                pack = CoinPack.WithExtraCoinEvent(pack, coin, coinOffset, evt, _pool);
                
                if(pack.SizeInBytes > _store.SectorSizeInBytes[pack.LayerIndex])
                    throw new SozuDataOverflowException("Last layer is overflowing.");

                _store.Write(new ShortCoinPackCollection(pack));

                // As the coin already exists, no need to update the filter on the first layer.
                return CoinChangeStatus.Success;
            }

            // We extend the probabilistic filter with a new signature.
            var pack0 = _store.Read(layerIndex: 0, sectorIndex);
            pack0 = CoinPack.WithExtraOutpointSig(pack0, sig, _pool);

            ShortCoinPackCollection packs;

            // The extended pack may overflow because of extra sig.
            if (pack0.SizeInBytes < _store.SectorSizeInBytes[0])
            {
                packs = new ShortCoinPackCollection(pack0);
            }
            else
            {
                packs = GetOverflow(pack0, lineage, _hash);

                // Edge-case, the layer layer has been impacted by the overflow.
                for (var i = 0; i < packs.Count; i++)
                {
                    if (packs[i].LayerIndex == pack.LayerIndex)
                    {
                        pack = packs[i];
                    }
                }
            }

            var newCoin = new Coin(ref outpoint, isCoinBase, payload, evt, _pool);
            var extendedPack = CoinPack.WithExtraCoin(pack, newCoin, _pool);

            packs.Add(extendedPack);

            _store.Write(packs);

            return CoinChangeStatus.Success;
        }

        /// <summary>
        /// Gets the coin packs resulting from the execution of the overflow.
        /// The underlying store is not written.
        /// </summary>
        private ShortCoinPackCollection GetOverflow(CoinPack pack, ILineage lineage, IOutpointHash hash)
        {
            Debug.Assert(pack.SizeInBytes >= _store.SectorSizeInBytes[pack.LayerIndex], "Overflow is expected.");

            var sectorIndex = pack.SectorIndex;

            var overflow = new ShortCoinPackCollection();

            var current = pack;
            do
            {
                if(current.LayerIndex > _store.LayerCount)
                    throw new SozuDataOverflowException("Last layer is overflowing.");

                current = CoinPack.WithPruning(current, lineage, hash, _pool, out var prunedSigs);
                if (current.LayerIndex > 0)
                {
                    overflow[0] = CoinPack.WithLessOutpointSigs(overflow[0], prunedSigs, _pool);
                }

                // If pruning is sufficient, no overflow is needed.
                if (current.SizeInBytes <= _store.SectorSizeInBytes[current.LayerIndex])
                    break;

                var movedCoinCount = current.CountCoinsAbove(OverflowChunkInBytes);
                var next = _store.Read(current.LayerIndex + 1, sectorIndex);

                // Removing coins from the current pack.
                var reduced = CoinPack.WithSkippedCoins(current, movedCoinCount, _hash, _pool);

                // Moving those coins to the next pack.
                var expanded = CoinPack.WithExtraCoins(next, current, 0, movedCoinCount, _pool);

                overflow.Add(reduced);

                current = expanded;

                // Next pack is also overflowing.
            } while (current.LayerIndex < _store.LayerCount - 1);
            
            overflow.Add(current);

            return overflow;
        }

        public CoinChangeStatus AddConsumption(ulong outpointHash, ref Outpoint outpoint, BlockAlias context,
            ILineage lineage)
        {
            try
            {
                var sectorIndex = (uint) (outpointHash % (uint) _store.SectorCount);
                var evt = new CoinEvent(context, CoinEventKind.Consumption);

                var extendedPack = CoinPack.Empty;

                for (var layerIndex = 0; layerIndex < _store.LayerCount; layerIndex++)
                {
                    // Check deeper layers
                    var packN = _store.Read(layerIndex: layerIndex, sectorIndex);
                    if (packN.TryGet(ref outpoint, out Coin coin, out int coinOffset))
                    {
                        if (coin.HasEvent(evt))
                            return CoinChangeStatus.Success;

                        if (!lineage.IsAddConsistent(coin.Events, evt))
                            return CoinChangeStatus.InvalidContext;

                        extendedPack = CoinPack.WithExtraCoinEvent(packN, coin, coinOffset, evt, _pool);
                        break;
                    }

                    if (layerIndex == _store.LayerCount - 1)
                    {
                        // The coin has to exist because a production came first.
                        return CoinChangeStatus.OutpointNotFound;
                    }
                }

                // Check of overflow
                var packs = extendedPack.SizeInBytes < _store.SectorSizeInBytes[extendedPack.LayerIndex]
                    ? new ShortCoinPackCollection(extendedPack)
                    : GetOverflow(extendedPack, lineage, _hash);

                // Persist the pack(s), several coin packs in case of overflow.
                _store.Write(packs);

                return CoinChangeStatus.Success;
            }
            finally
            {
                _pool.Reset();
            }
        }

        public CoinChangeStatus Remove(ulong outpointHash, ref Outpoint outpoint, BlockAlias context,
            CoinRemoveOption option, ILineage lineage)
        {
            var sectorIndex = (uint) (outpointHash % (uint) _store.SectorCount);

            try
            {
                for (var layerIndex = 0; layerIndex < _store.LayerCount; layerIndex++)
                {
                    var packN = _store.Read(layerIndex: layerIndex, sectorIndex);
                    if (packN.TryGet(ref outpoint, out Coin coin, out int coinOffset))
                    {
                        var eventsFound = lineage.TryGetEventsInContext(coin.Events, context, out var readTempProduction,
                            out var readTempConsumption);

                        if (!eventsFound)
                        {
                            return CoinChangeStatus.InvalidContext;
                        }

                        if (option.HasFlag(CoinRemoveOption.RemoveConsumption) && !readTempConsumption.IsUndefined)
                        {
                            coin = Coin.WithLessEvent(coin,
                                new CoinEvent(readTempConsumption, CoinEventKind.Consumption), _pool);
                        }

                        if (option.HasFlag(CoinRemoveOption.RemoveProduction) && !readTempProduction.IsUndefined)
                        {
                            coin = Coin.WithLessEvent(coin, new CoinEvent(readTempProduction, CoinEventKind.Production),
                                _pool);
                        }

                        var reducedPack = CoinPack.WithLessCoinEvent(packN, coin, coinOffset, _pool);
                        _store.Write(new ShortCoinPackCollection(reducedPack));

                        return CoinChangeStatus.Success;
                    }
                }

                return CoinChangeStatus.OutpointNotFound;
            }
            finally 
            {
                _pool.Reset();
            }
           
        }
    }
}
