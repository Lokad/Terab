using System;
using System.Diagnostics;
using System.IO;
using Terab.UTXO.Core.Helpers;
using Terab.UTXO.Core.Sozu;

namespace Terab.UTXO.Core.SectorStore
{
    /// <summary>
    /// Store data in-memory until persistence is requested. Then, all is 
    /// flushed to persistent storage. Intended to reduce the I/O pressure 
    /// on persistent storage, leveraging the weak durability guarantees 
    /// of Terab.
    /// </summary>
    /// <remarks>
    /// This store is intended for small amount of data that can, not only be
    /// stored in memory but, be flushed to persistent storage reasonably fast.
    /// 
    /// When persistence is requested, the whole store is dumped to a file.
    /// Then, an pseudo-atomic swap of files is used in order to ensure that
    /// a power cycle cannot corrupt the data.
    /// </remarks>
    public class VolatileStore : ISectorStore
    {
        private readonly int _sectorSizeInBytes;

        /// <summary>
        /// Point to a file intended as the primary recipient of the store data.
        /// </summary>
        private readonly string _storePath;

        private readonly ILog _log;

        /// <summary>Number of sectors allocated by the store.</summary>
        private SectorIndex _firstNotAllocated;

        /// <summary>Single instance, re-used.</summary>
        private readonly Sector _sector;

        /// <summary>
        /// Persistent data storage (where the memory stream is 
        /// dumped when persistence is requested).
        /// </summary>
        private FileStream _storeStream;

        /// <summary>Volatile data storage.</summary>
        private MemoryStream _memoryStream;

        /// <summary>
        /// Temporary file path, used to persist a volatile store.
        /// </summary>
        private string TmpStorePath => _storePath + ".tmp";

        public VolatileStore(int sectorSizeInBytes, string storePath, ILog log = null)
        {
            _sectorSizeInBytes = sectorSizeInBytes;
            _storePath = storePath;
            _firstNotAllocated = SectorIndex.Unknown; // not initialized
            _log = log;
            _sector = new Sector(_sectorSizeInBytes);
        }

        public void Initialize()
        {
            // recovery on edge crash
            if (File.Exists(TmpStorePath) && !File.Exists(_storePath))
            {
                File.Move(TmpStorePath, _storePath);
                _log.Log(LogSeverity.Warning, $"File recovered: {TmpStorePath}");
            }

            // Number of sectors is inferred from the file size.
            _firstNotAllocated = SectorIndex.First;
            if (File.Exists(_storePath))
            {
                var info = new FileInfo(_storePath);
                _firstNotAllocated = SectorIndex.OffsetInBytes(info.Length, _sectorSizeInBytes);
            }

            // hot stream does not apply to volatile store
            if (!File.Exists(_storePath))
            {
                _storeStream = new FileStream(_storePath, FileMode.Create, FileAccess.ReadWrite, FileShare.None);
                _log?.Log(LogSeverity.Info, $"Empty file created: {_storePath}.");
                _firstNotAllocated = SectorIndex.First;

                _memoryStream = new MemoryStream();
            }
            else
            {
                _storeStream = new FileStream(_storePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                _memoryStream = new MemoryStream((int)_storeStream.Length);
                _storeStream.CopyTo(_memoryStream);
            }
        }

        public bool HasSectors()
        {
            return _firstNotAllocated.Value != SectorIndex.First.Value;
        }

        public void Dispose()
        {
            _storeStream?.Dispose();
            _memoryStream?.Dispose();
        }

        public SectorIndex AllocateSectors(int count)
        {
            if (count <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }

            var firstNewlyAllocated = _firstNotAllocated;
            _firstNotAllocated = _firstNotAllocated.GetNext(count);

            _memoryStream.SetLength(_memoryStream.Length + _sectorSizeInBytes * count);
            _sector.Clear();
            for (var idx = firstNewlyAllocated;
                idx.IsWithinRange(_firstNotAllocated);
                idx = idx.GetNext())
            {
                _sector.Write(_memoryStream, idx.ByteIndex(_sectorSizeInBytes));
            }

            Debug.Assert(_memoryStream.Length == _firstNotAllocated.ByteIndex(_sectorSizeInBytes));

            return firstNewlyAllocated;
        }

        /// <summary>
        /// The returned 'Sector' is invalidated at the next call.
        /// </summary>
        public Sector Read(SectorIndex index)
        {
            _memoryStream.Seek(index.ByteIndex(_sectorSizeInBytes), SeekOrigin.Begin);
            _sector.Read(_memoryStream, index.ByteIndex(_sectorSizeInBytes));

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

        public void Write(SectorIndex index, Sector source)
        {
            _memoryStream.Seek(index.ByteIndex(_sectorSizeInBytes), SeekOrigin.Begin);
            source.Write(_memoryStream, index.ByteIndex(_sectorSizeInBytes));
        }

        public void EnsurePersistence()
        {
            using (var stream = new FileStream(TmpStorePath, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
            {
                _memoryStream.Seek(0, SeekOrigin.Begin);
                _memoryStream.CopyTo(stream);
            }

            _storeStream.Dispose();

            // pseudo atomic swap
            File.Delete(_storePath);
            File.Move(TmpStorePath, _storePath);

            _storeStream = new FileStream(_storePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        }
    }
}
