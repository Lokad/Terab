using System;
using System.IO;
using Terab.UTXO.Core.Hash;
using Terab.UTXO.Core.SectorStore;

namespace Terab.UTXO.Core.Sozu
{
    /// <summary>
    /// A physical storage block, referred as a sector
    /// in order to avoid confusion with the Bitcoin blocks.
    /// </summary>
    /// <remarks>
    /// The sector is organized as follow:
    /// - 8 bytes for the index key (potentially self-referential, in bytes)
    /// - n bytes for the body
    /// - 16 bytes for the checksum, MurmurHash3 (128 bits)
    /// </remarks>
    public class Sector
    {
        /// <summary>
        /// Intended to ensure the integrity of the content stored in the
        /// persistent data storage device.
        /// </summary>
        private readonly MurmurHash3 _hashingFunction;

        private const int IndexSizeInBytes = sizeof(long);
        private const int HeaderSizeInBytes = Hash128.SizeInBytes + IndexSizeInBytes;

        private readonly byte[] _sectorData;

        public int Length => _sectorData.Length;

        public Sector(int sectorSizeInBytes)
        {
            // a sector should contain at least a long index and a checksum
            if (sectorSizeInBytes <= HeaderSizeInBytes)
                throw new ArgumentException("Sector size to narrow.");

            _hashingFunction = new MurmurHash3();

            _sectorData = new byte[sectorSizeInBytes];
        }


        public Span<byte> Contents =>
            _sectorData.AsSpan(IndexSizeInBytes, _sectorData.Length - HeaderSizeInBytes);

        public void Clear()
        {
            Contents.Clear();
            BitConverter.TryWriteBytes(Contents.Slice(0, IndexSizeInBytes), SectorIndex.Unknown.Value);
        }

        /// <summary>
        /// Read from stream without index verification.
        /// </summary>
        /// <remarks>
        /// The stream is already provided with the correct state.
        /// It just needs to be read for the length of the buffer.
        /// Apart from the checksum, no verifications are executed.
        /// </remarks>
        /// <returns>The stored index is returned.</returns>
        public long Read(Stream stream)
        {
            var readDataLength = stream.Read(_sectorData, 0, _sectorData.Length);
            if (readDataLength < _sectorData.Length)
            {
                throw new BufferPartiallyFilledException(
                    $"Only {readDataLength} bits were read instead of {_sectorData.Length}.");
            }

            VerifyChecksum();

            return BitConverter.ToInt64(new ReadOnlySpan<byte>(_sectorData, 0, IndexSizeInBytes));
        }

        /// <summary>
        /// Read from stream with index verification.
        /// </summary>
        /// <remarks>
        /// The stream is already provided with the correct state.
        /// It just needs to be read for the length of the buffer.
        /// Apart from the hash, the index is also verified as it is expected
        /// to be self-referential.
        /// </remarks>
        /// <exception cref="InvalidDataException">
        /// If the stored index is not equal to <paramref name="expectedIndex"/>.
        /// </exception>
        public void Read(Stream stream, long expectedIndex)
        {
            var storedIndex = Read(stream);

            if (storedIndex == expectedIndex)
                return;

            throw new SectorInvalidDataException("Sector index mismatch");
        }

        /// <summary>
        /// This method compute the checksum of the stored data.
        /// </summary>
        private Hash128 ComputeChecksum()
        {
            return _hashingFunction.ComputeHash(new Span<byte>(_sectorData, 0, _sectorData.Length - Hash128.SizeInBytes));
        }

        /// <summary>
        /// This method verifies that the data has not been corrupted by
        /// computing its checksum and comparing it with the one stored
        /// with the data.
        /// </summary>
        /// <exception cref="CorruptedStorageException">
        /// Exception is thrown when the checksum mismatch and the data is
        /// most probably corrupted.
        /// </exception>
        private void VerifyChecksum()
        {
            var freshChecksum = ComputeChecksum();
            var originalChecksum = new Hash128(_sectorData.AsSpan(_sectorData.Length - Hash128.SizeInBytes));

            if (!originalChecksum.Equals(freshChecksum))
                throw new CorruptedStorageException();
        }

        public void Write(Stream stream, long index)
        {
            BitConverter.TryWriteBytes(new Span<byte>(_sectorData, 0, IndexSizeInBytes), index);
            var currentChecksum = ComputeChecksum();
            currentChecksum.WriteTo(new Span<byte>(_sectorData).Slice(_sectorData.Length - Hash128.SizeInBytes));

            stream.Write(_sectorData, 0, _sectorData.Length);
        }
    }
}