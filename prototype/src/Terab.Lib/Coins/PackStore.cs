// Copyright Lokad 2018 under MIT BCH.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using Terab.Lib.Coins.Exceptions;

namespace Terab.Lib.Coins
{
    /// <summary>
    /// Implementation of <see cref="IPackStore"/> using memory mapped files.
    /// </summary>
    /// <remarks>
    /// This implementation contains multiple layers of
    /// <see cref="MemoryMappedFileSlim"/>, and also a journal.
    ///
    /// Journal is used only when multiple layers are modified at once (typically,
    /// during an overflow). Every layer is divided into fixed size sectors, each
    /// layer has its own sector size. However, all layers have same count number
    /// of sectors.
    ///
    /// When all layers of a particular sector are full, a generic key value
    /// store <see cref="_bottomLayer"/> is used for handling overflowed
    /// data.
    /// </remarks>
    public class PackStore : IPackStore
    {
        private const int DefaultBottomLayerSectorSize = 512;

        private readonly int _sectorCount;

        /// <summary> Sector size for each layer. </summary>
        private readonly int[] _sectorSizeInBytes;

        /// <summary> Intermediate layers. </summary>
        private readonly MemoryMappedFileSlim[] _layerMmf;

        /// <summary> Final layer. </summary>
        private readonly IKeyValueStore<uint> _bottomLayer;

        private readonly MemoryMappedFileSlim _journalMmf;

        private byte _currentColor;

        public int SectorCount => _sectorCount;

        public int LayerCount => _sectorSizeInBytes.Length + 1;

        public IReadOnlyList<int> SectorSizeInBytes => _sectorSizeInBytes;

        private ILog _log;

        /// <summary> Ctor. </summary>
        /// <param name="sectorCount">  Count number of sectors in a single layer. </param>
        /// <param name="sectorSizeInBytes"> Array of each layer sector size in bytes. </param>
        /// <param name="layerMmf"> Backing all layers but the bottom one. </param>
        /// <param name="journalMmf"> Backing the journal. </param>
        /// <param name="bottomLayer"> Backing the last layer, with a lazy sector allocation. </param>
        /// <param name="log"></param>
        public PackStore(
            int sectorCount,
            int[] sectorSizeInBytes,
            MemoryMappedFileSlim[] layerMmf,
            MemoryMappedFileSlim journalMmf,
            IKeyValueStore<uint> bottomLayer,
            ILog log = null)
        {
            if (sectorSizeInBytes.Length != layerMmf.Length)
                throw new ArgumentException("Arguments length mismatch.",
                    nameof(sectorSizeInBytes) + nameof(layerMmf));

            _sectorSizeInBytes = sectorSizeInBytes;
            _sectorCount = sectorCount;

            _layerMmf = layerMmf;
            _journalMmf = journalMmf;
            _bottomLayer = bottomLayer;
            _log = log;
        }

        public void Initialize()
        {
            for (var layerIndex = 0; layerIndex < _layerMmf.Length; layerIndex++)
            {
                var sectorSize = _sectorSizeInBytes[layerIndex];
                var layer = _layerMmf[layerIndex];

                // If last sector is already initialized, then skip the whole layer.
                var version = new CoinPack(layer.GetSpan(layer.FileLength - sectorSize, sectorSize)).Version;
                if (version == CoinPack.CurrentVersion)
                    continue;

                if(version != 0)
                    throw new NotSupportedException($"Unknown CoinPack version: {version}.");

                _log?.Log(LogSeverity.Info, $"Initialize {layer.FileName} over {layer.FileLength} bytes.");

                for (long offset = 0; offset < layer.FileLength; offset += sectorSize)
                {
                    var span = layer.GetSpan(offset, sectorSize);
                    var coinpack = new CoinPack(span);

                    // If sector is not initialized, then initialize.
                    if (coinpack.Version != 0)
                        continue;

                    coinpack.Version = CoinPack.CurrentVersion;
                    coinpack.SectorIndex = (uint) (offset / sectorSize);
                    coinpack.LayerIndex = (byte) layerIndex;
                }

                layer.Flush();
            }
        }

        private Span<byte> GetSpan(int layerIndex, uint sectorIndex)
        {
            if (layerIndex >= _layerMmf.Length)
            {
                if(_bottomLayer == null)
                    throw new SozuDataOverflowException("Bottom layer is not defined, can't overflow.");

                if (!_bottomLayer.TryGet(sectorIndex, out var span))
                {
                    // In case where key sectorIndex doesn't exist,
                    // we initialize a empty array as value for this key.
                    // We can see this as lazy sector allocation (only
                    // for the bottom layer).

                    Span<byte> mem = stackalloc byte[DefaultBottomLayerSectorSize];
                    var pack = new CoinPack(mem)
                    {
                        SectorIndex = sectorIndex,
                        LayerIndex = (byte) layerIndex,
                        Version = CoinPack.CurrentVersion,
                    };

                    _bottomLayer.Set(sectorIndex, mem);
                    _bottomLayer.TryGet(sectorIndex, out span);
                }

                return span;
            }

            return _layerMmf[layerIndex]
                .GetSpan(sectorIndex * _sectorSizeInBytes[layerIndex], _sectorSizeInBytes[layerIndex]);
        }

        public CoinPack Read(int layerIndex, uint sectorIndex)
        {
            var span = GetSpan(layerIndex, sectorIndex);

            var pack = new CoinPack(span);

            if (pack.Version == 0)
                throw new SozuCorruptedDataException("Sector not initialized.");

            if (pack.SectorIndex != sectorIndex || pack.LayerIndex != layerIndex)
                throw new SozuCorruptedDataException("SectorIndex mismatch or LayerIndex mismatch.");

            return pack;
        }


        /// <summary>
        /// 2 steps write: firstly write into journal, then write into
        /// each layer storage.
        /// </summary>
        /// <remarks>
        /// <see cref="_currentColor"/> helps to check journal file integrity.
        /// <seealso cref="CheckJournalIntegrity"/>
        /// </remarks>
        public void Write(ShortCoinPackCollection coll)
        {
            // Short-circuit journalization when data storage hardware allows.
            if (Constants.SkipJournalizationWithAtomicWrite &&
                coll.Count == 1 && coll[0].LayerIndex == 0)
            {
                var pack = coll[0];
                pack.Span.CopyTo(GetSpan(pack.LayerIndex, pack.SectorIndex));
                _layerMmf[pack.LayerIndex].FlushLastSpan();

                return;
            }

            // Write journal
            {
                var spanLength = 0;
                for (var i = 0; i < coll.Count; ++i)
                    spanLength += coll[i].SizeInBytes;

                var journalSpan = _journalMmf.GetSpan(0, spanLength);

                for (var i = 0; i < coll.Count; ++i)
                {
                    var pack = coll[i];
                    pack.WriteColor = _currentColor;
                    pack.WriteCount = (byte) (coll.Count - i); // write count is decreasing

                    pack.Span.CopyTo(journalSpan);
                    journalSpan = journalSpan.Slice(pack.SizeInBytes);
                }

                _journalMmf.FlushLastSpan();
            }

            // Write main storage
            for (var i = 0; i < coll.Count; ++i)
            {
                var pack = coll[i];
                //get pack from physical storage. 

                Debug.Assert(pack.LayerIndex <= _layerMmf.Length);
                if (pack.LayerIndex == _layerMmf.Length)
                {
                    _bottomLayer.Set(pack.SectorIndex, pack.Span);
                }
                else
                {
                    pack.Span.CopyTo(GetSpan(pack.LayerIndex, pack.SectorIndex));
                    _layerMmf[pack.LayerIndex].FlushLastSpan();
                }
            }

            _currentColor = (byte) unchecked(_currentColor + 1);
        }

        /// <summary>
        /// Returns false if journal data integrity is broken, either color
        /// code is broken or writecount is broken.
        /// </summary>
        public bool CheckJournalIntegrity()
        {
            int offset = 0;

            // check the very first pack
            var pack = new CoinPack(_journalMmf.GetSpan(offset, Constants.CoinStoreJournalCapacity));
            byte count = pack.WriteCount;
            byte lastCount = count;
            byte color = pack.WriteColor;
            int size = pack.SizeInBytes;
            offset += size;

            // check next packs
            for (var i = 1; i < count; ++i)
            {
                pack = new CoinPack(_journalMmf.GetSpan(offset, Constants.CoinStoreJournalCapacity - offset));
                if (pack.WriteColor != color || pack.WriteCount + 1 != lastCount)
                    return false;

                lastCount = pack.WriteCount;
                size = pack.SizeInBytes;
                offset += size;
            }

            Debug.Assert(lastCount == 1);

            // all colors are the same, and write count decreased 1 each time, until it reaches 1
            // we consider that journal data integrity is preserved.
            return true;
        }

        /// <summary>
        /// Use journal data to recover data in layered storages.
        /// </summary>
        public void RecoverFromJournal()
        {
            int offset = 0;

            // extract count and first size from the very first pack
            var pack = new CoinPack(_journalMmf.GetSpan(offset, Constants.CoinStoreJournalCapacity));
            byte count = pack.WriteCount;
            int size = pack.SizeInBytes;

            // iterate through packs in journal by exact slicing
            for (var i = 0; i < count; ++i)
            {
                pack = new CoinPack(_journalMmf.GetSpan(offset, Constants.CoinStoreJournalCapacity - offset));

                Debug.Assert(pack.LayerIndex <= _layerMmf.Length);
                if (pack.LayerIndex == _layerMmf.Length)
                {
                    _bottomLayer.Set(pack.SectorIndex, pack.Span);
                }
                else
                {
                    pack.Span.CopyTo(GetSpan(pack.LayerIndex, pack.SectorIndex));
                    _layerMmf[pack.LayerIndex].FlushLastSpan();
                }

                size = pack.SizeInBytes;
                offset += size;
            }
        }

        /// <summary>
        /// Returns true if journal is coherent to corresponding layered
        /// storages, false otherwise.
        /// </summary>
        public bool CompareWithJournal()
        {
            int offset = 0;

            // extract count and first size from the very first pack
            var pack = new CoinPack(_journalMmf.GetSpan(offset, Constants.CoinStoreJournalCapacity));
            byte count = pack.WriteCount;
            int size = pack.SizeInBytes;

            // iterate through packs in journal by exact slicing
            for (var i = 0; i < count; ++i)
            {
                pack = new CoinPack(_journalMmf.GetSpan(offset, Constants.CoinStoreJournalCapacity - offset));

                try
                {
                    var storedPack = Read(pack.LayerIndex, pack.SectorIndex);

                    if (!pack.Span.SequenceEqual(storedPack.Span))
                        return false;

                    size = pack.SizeInBytes;
                    offset += size;
                }
                catch (SozuCorruptedDataException)
                {
                    return false;
                }
            }

            return true;
        }

        public void Dispose()
        {
            for (var i = 0; i < _sectorSizeInBytes.Length; ++i)
            {
                _layerMmf[i].Dispose();
            }

            _journalMmf.Dispose();
        }
    }
}