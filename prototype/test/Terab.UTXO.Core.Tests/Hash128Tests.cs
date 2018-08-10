using System;
using Terab.UTXO.Core.Hash;
using Xunit;

namespace Terab.UTXO.Core.Tests
{
    public class Hash128Tests
    {
        private byte[] _hashBytes;
        private Hash128 _hash;

        private void PrepareHash()
        {
            if (_hashBytes == null)
            {
                var rnd = new Random(42);
                _hashBytes = new byte[Hash128.SizeInBytes];
                rnd.NextBytes(_hashBytes);
                _hash = new Hash128(new ReadOnlySpan<byte>(_hashBytes, 0, Hash128.SizeInBytes));
            }
        }

        [Fact]
        public void HashConversions()
        {
            PrepareHash();

            var convertedHash = new byte[Hash128.SizeInBytes];
            _hash.WriteTo(new Span<byte>(convertedHash, 0, Hash128.SizeInBytes));
            //Console.WriteLine(BitConverter.ToString(hashBytes));
            //Console.WriteLine(BitConverter.ToString(convertedHash));
            Assert.Equal(_hashBytes, convertedHash);
        }

        [Fact]
        public void HashWriteException()
        {
            PrepareHash();
            var convertedHash = new byte[Hash128.SizeInBytes - 1];
            Assert.Throws<IndexOutOfRangeException>(() =>
                _hash.WriteTo(new Span<byte>(convertedHash, 0, Hash128.SizeInBytes - 1)));
        }

        [Fact]
        public void EndiannessHash128()
        {
            // A sequence of bytes, all different, each line fills an entire ulong
            var bytes = new byte[]
            {
                0x11, 0x12, 0x13, 0x14, 0x15, 0x16, 0x17, 0x18,
                0x21, 0x22, 0x23, 0x24, 0x25, 0x26, 0x27, 0x28
            };

            var hash = new Hash128(bytes);

            // Did the bytes move to the correct locations in the four ulongs ? 
            Assert.Equal(0x1112131415161718UL, hash.Left);
            Assert.Equal(0x2122232425262728UL, hash.Right);

            // Does ToString() display the correct overall hash, in hex ?
            Assert.Equal(
                "11121314151617182122232425262728",
                hash.ToString());

            // Does writing out the hash back to a byte array restore the right order ?
            var write = new byte[Hash128.SizeInBytes];
            hash.WriteTo(write);

            Assert.Equal(bytes, write);
        }
    }
}