using System;
using System.Diagnostics;
using System.IO;
using Terab.UTXO.Core.Helpers;
using Terab.UTXO.Core.Sozu;

namespace Terab.UTXO.Core.SectorStore
{
    /// <summary>
    /// Persist sectors with a 2-phase write that ensures that the
    /// in-place write do not corrupt the store by merely failing
    /// halfway through the write operation.
    /// </summary>
    /// <remarks>
    /// The storage leverages in-place overwrites for performance.
    /// As a result, there is a risk of corrupting the existing data
    /// if a power loss happen while a write is in progress.
    /// 
    /// In order to eliminate this problem, the store leverages a
    /// 2-phase write pattern where:
    /// 
    /// 1) Write sector to the "hot" store. Ensure write commit.
    /// 2) Write sector to the regular store. Ensure write commit.
    /// 
    /// The sector is self-referential, hence, upon restart the store
    /// can check whether the last write has completed successfully.
    /// 
    /// This design is intended to take advantage of high I/O device
    /// like the Intel Optane which do not suffer from wear-leveling.
    /// </remarks>
    public class TwoPhaseStore : ISectorStore
    {
        /// <summary>
        /// Points to a file located on a high I/O and low wear-leveling device.
        /// </summary>
        private readonly string _hotPath;

        private readonly ILog _log;

        /// <summary>Single instance, re-used.</summary>
        private readonly Sector _sector;

        private readonly int _sectorSizeInBytes;

        /// <summary>
        /// Point to a file intended as the primary recipient of the store data.
        /// </summary>
        private readonly string _storePath;

        private FileStream _hotStream;

        /// <summary>
        /// Index of the first not allocated sector by the store.
        /// </summary>
        private SectorIndex _firstNotAllocated;

        /// <summary>Persistent data storage.</summary>
        private FileStream _storeStream;


        public TwoPhaseStore(int sectorSizeInBytes, string storePath, ILog log = null)
        {
            _sectorSizeInBytes = sectorSizeInBytes;
            _hotPath = Path.Combine(Path.GetDirectoryName(storePath), @"hot-" + Path.GetFileName(storePath));
            _storePath = storePath;
            _firstNotAllocated = SectorIndex.Unknown; // dummy initialization
            _log = log;
            _sector = new Sector(_sectorSizeInBytes);
        }


        public void Initialize()
        {
            if (!File.Exists(_storePath))
            {
                _storeStream = new FileStream(_storePath, FileMode.Create, FileAccess.ReadWrite, FileShare.None);
                _firstNotAllocated = SectorIndex.First;
                _log?.Log(LogSeverity.Info, $"Empty file created: {_storePath}.");
            }
            else
            {
                _storeStream = new FileStream(_storePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            }

            // Number of sectors is inferred from the file size.
            var info = new FileInfo(_storePath);

            _firstNotAllocated = SectorIndex.OffsetInBytes(info.Length, _sectorSizeInBytes);

            if (!File.Exists(_hotPath))
            {
                _hotStream = new FileStream(_hotPath, FileMode.Create, FileAccess.ReadWrite, FileShare.None);
                _log?.Log(LogSeverity.Info, $"Empty file created: {_hotPath}.");
            }
            else
            {
                _hotStream = new FileStream(_hotPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                TryRecovery();
            }
        }

        public bool HasSectors()
        {
            return _firstNotAllocated.Value != SectorIndex.First.Value;
        }

        public void Dispose()
        {
            _hotStream?.Dispose();
            _storeStream?.Dispose();
        }

        public SectorIndex AllocateSectors(int count)
        {
            if (count <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }

            var firstNewlyAllocated = _firstNotAllocated;
            _firstNotAllocated = _firstNotAllocated.GetNext(count);

            _storeStream.SetLength(_storeStream.Length + _sectorSizeInBytes * count);

            _sector.Clear();

            for (var idx = firstNewlyAllocated; 
                idx.IsWithinRange(_firstNotAllocated); 
                idx = idx.GetNext())
            {
                _sector.Write(_storeStream, idx.ByteIndex(_sectorSizeInBytes));
            }

            Debug.Assert(_storeStream.Length == _firstNotAllocated.ByteIndex(_sectorSizeInBytes));

            return firstNewlyAllocated;
        }

        /// <remarks>
        /// The returned 'Sector' is invalidated at the next call.
        /// </remarks>
        public Sector Read(SectorIndex index)
        {
            if (!index.IsWithinRange(_firstNotAllocated))
                throw new ArgumentOutOfRangeException(
                    $"Try to read from index {index} out of total {_firstNotAllocated.Value} sectors.");

            _storeStream.Seek(index.ByteIndex(_sectorSizeInBytes), SeekOrigin.Begin);
            _sector.Read(_storeStream, index.ByteIndex(_sectorSizeInBytes));

            return _sector;
        }

        public bool TryRead(SectorIndex index, out Sector sector)
        {
            sector = null;

            // upper layer saved -1 as sectorIndex if child sector doesn't exist.
            if (index.IsUnknown())
                return false;

            var position = _storeStream.Seek(index.ByteIndex(_sectorSizeInBytes), SeekOrigin.Begin);

            // seek a position beyond file length
            if (position == _storeStream.Length)
                return false;

            _sector.Read(_storeStream, index.ByteIndex(_sectorSizeInBytes));
            sector = _sector;
            return true;
        }

        /// <summary>
        /// Write to hot storage first, then regular storage secondly.
        /// </summary>
        public void Write(SectorIndex index, Sector source)
        {
            if (!index.IsWithinRange(_firstNotAllocated))
                throw new ArgumentOutOfRangeException(
                    $"Try to write to index {index.Value} out of total {_firstNotAllocated.Value} sectors.");

            _hotStream.Seek(0, SeekOrigin.Begin);
            source.Write(_hotStream, index.ByteIndex(_sectorSizeInBytes));

            _storeStream.Seek(index.ByteIndex(_sectorSizeInBytes), SeekOrigin.Begin);
            source.Write(_storeStream, index.ByteIndex(_sectorSizeInBytes));
        }

        public void EnsurePersistence()
        {
            // Nothing to be done here. The durability of the
            // two-phase store is ensured after every single
            // write.
        }

        /// <summary>
        /// Recover a halfway written sector.
        /// </summary>
        /// <remarks>
        /// TODO: verify this method does what it is supposed to do, after replacing TryRead with a Read that throws exceptions,
        /// instead of returning a bool.
        /// </remarks>
        private void TryRecovery()
        {
            try
            {
                var offset = _sector.Read(_hotStream); // hotstream is valid

                var deducedIndex = SectorIndex.OffsetInBytes(offset, _sectorSizeInBytes);
                try
                {
                    //try read last recorded sector

                    Read(deducedIndex);
                    // reach here means that both streams are ok
                }
                catch (SectorOperationException)
                {
                    // something went wrong in storeStream
                    _hotStream.Seek(0, SeekOrigin.Begin);
                    _sector.Read(_hotStream, offset);
                    // reach here means that hostream is ok

                    // copying '_hotstream' back to '_storeStream'
                    _storeStream.Seek(deducedIndex.ByteIndex(_sectorSizeInBytes), SeekOrigin.Begin);
                    _sector.Write(_storeStream, deducedIndex.ByteIndex(_sectorSizeInBytes));

                    // TODO: add log to indicate successful recovery.
                }
            }
            catch (SectorOperationException)
            {
                // hotstream invalid
                // normally thanks to double writing, main store stream is valid
                _log?.Log(LogSeverity.Warning, "Hotstream error when trying to recover sector store.");

                throw;
            }
        }
    }
}