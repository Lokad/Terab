using System;
using System.IO;
using Terab.UTXO.Core.Hash;
using Terab.UTXO.Core.SectorStore;
using Terab.UTXO.Core.Sozu;
using Xunit;

namespace Terab.UTXO.Core.Tests
{
    public class SectorTests
    {
        private readonly Sector _sector;
        private readonly MemoryStream _stream;
        private readonly long _key;
        private readonly byte[] _contentBytes;
        public const int SectorSize = 40;

        public SectorTests()
        {
            _sector = new Sector(SectorTests.SectorSize); // new sector

            var content = _sector.Contents; // filling the content part of the sector
            var rnd = new Random(42);
            _contentBytes = new byte[content.Length];
            rnd.NextBytes(_contentBytes);
            for (int i = 0; i < _contentBytes.Length; i++)
            {
                content[i] = _contentBytes[i];
            }

            _key = LongRandom(rnd); // inventing a key
            _stream = new MemoryStream(new byte[SectorTests.SectorSize]); // creating a stream to write into


            long LongRandom(Random rand)
            {
                byte[] buf = new byte[8];
                rand.NextBytes(buf);
                return BitConverter.ToInt64(buf, 0);
            }
        }

        [Fact]
        public void WriteSector()
        {
            // Writing to the stream and verify that the written data is correct.
            _sector.Write(_stream, _key);
            var actual = _stream.ToArray();

            var expected = new byte[SectorSize];
            BitConverter.TryWriteBytes(new Span<byte>(expected, 0, sizeof(long)), _key);
            for (int i = 0; i < _contentBytes.Length; i++)
            {
                expected[i + sizeof(long)] = _contentBytes[i];
            }

            var hashValue =
                new MurmurHash3().ComputeHash(new Span<byte>(expected, 0, expected.Length - Hash128.SizeInBytes));
            hashValue.WriteTo(new Span<byte>(expected).Slice(expected.Length - Hash128.SizeInBytes));
            Console.WriteLine(BitConverter.ToString(expected));
            Console.WriteLine(BitConverter.ToString(actual));
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void WriteSectorBytes()
        {
            // A sequence of bytes, all different, each line fills an entire ulong
            var expected = new byte[]
            {
                0x18, 0x17, 0x16, 0x15, 0x14, 0x13, 0x12, 0x11, //key in inverse order, because stored in little endian
                0x11, 0x22, 0x33, 0x44, 0x55, 0x66, 0x77, 0x88, // content of 40 - 16 (Hash) - 8 (long) = 16 bytes
                0x91, 0x92, 0x93, 0x94, 0x95, 0x96, 0x97, 0x98,
                0xC9, 0x38, 0xD2, 0x67, 0x55, 0x59, 0xB5, 0x11, // hash
                0xAE, 0x21, 0xEA, 0xF7, 0x16, 0x41, 0x2C, 0xE0
            };

            var content = _sector.Contents;
            for (int i = 0; i < _contentBytes.Length; i++)
            {
                content[i] = expected[i + 8];
            }

            _sector.Write(_stream, 0x1112131415161718L);

            Console.WriteLine(BitConverter.ToString(expected));
            Console.WriteLine(BitConverter.ToString(_stream.ToArray()));

            Assert.Equal(expected, _stream.ToArray());
        }

        [Fact]
        public void WriteReadSector()
        {
            // Writing to the stream
            _sector.Write(_stream, _key);

            // Reading from the stream while providing the key
            _stream.Seek(0, SeekOrigin.Begin);
            _sector.Read(_stream, _key);
            Assert.Equal(_contentBytes, _sector.Contents.ToArray());

            // reading without providing a key and verifying that the correct one was returned
            _stream.Seek(0, SeekOrigin.Begin);
            var returnedKey = _sector.Read(_stream);
            Assert.Equal(_key, returnedKey);

            // verifying that the content that was written and that was read is the same
            // Console.WriteLine(BitConverter.ToString(contentBytes));
            // Console.WriteLine(BitConverter.ToString(sector.Contents.ToArray()));
            Assert.Equal(_contentBytes, _sector.Contents.ToArray());
        }

        [Fact]
        public void ReadWriteSectorWithError()
        {
            // Stream needs to be written so that the next step can fail. If the write fails,
            // the test should fail because the error is different from the invalidDataException.
            _sector.Write(_stream, _key);
            // Testing that an exception is thrown when the key is provided but does not match.
            _stream.Seek(0, SeekOrigin.Begin);
            Assert.Throws<SectorInvalidDataException>(() => _sector.Read(_stream, _key + 1));
        }

        [Fact]
        public void ReadCorruptedHash()
        {
            // Stream needs to be written so that the next step can fail. If the write fails,
            // the test should fail because the error is different from the invalidDataException.
            _sector.Write(_stream, _key);
            // Modifying the written hash
            _stream.Seek(_contentBytes.Length - 5, SeekOrigin.Begin);
            _stream.WriteByte(0);
            // Testing that an exception is thrown when the key is provided but does not match.
            _stream.Seek(0, SeekOrigin.Begin);
            Assert.Throws<CorruptedStorageException>(() => _sector.Read(_stream));
        }

        [Fact]
        public void ReadCorruptedHashWrongKey()
        {
            // Stream needs to be written so that the next step can fail. If the write fails,
            // the test should fail because the error is different from the invalidDataException.
            _sector.Write(_stream, _key);
            // Modifying the written hash
            _stream.Seek(_contentBytes.Length - 5, SeekOrigin.Begin);
            _stream.WriteByte(0);
            // Testing that an exception is thrown when the key is provided but does not match.
            _stream.Seek(0, SeekOrigin.Begin);
            Assert.Throws<CorruptedStorageException>(() => _sector.Read(_stream, _key + 1));
        }

        [Fact]
        public void ShortStream()
        {
            // Testing that an exception is thrown when the stream is shorter than the content.
            var rnd = new Random(42);
            var shortStreamContent = new byte[SectorSize - 1];
            rnd.NextBytes(shortStreamContent);

            var shortStream = new MemoryStream(shortStreamContent);
            Assert.Throws<BufferPartiallyFilledException>(() => _sector.Read(shortStream));
        }

        [Fact]
        public void CursorAtEndOfStream()
        {
            // Stream needs to be written so that the next step can fail. If the write fails,
            // the test should fail because the error is different from the DataMisalignedException.
            _sector.Write(_stream, _key);
            // Testing that an exception is thrown when the stream cannot read the required amount
            // of data because the cursor is not in the right place.
            _stream.Seek(_stream.Length, SeekOrigin.Begin);
            Assert.Throws<BufferPartiallyFilledException>(() => _sector.Read(_stream));
        }
    }
}