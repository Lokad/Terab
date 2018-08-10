using System;
using Terab.UTXO.Core.Hash;
using Xunit;

namespace Terab.UTXO.Core.Tests
{
    public class Hash256Tests
    {
        private byte[] _hashBytes;
        private Hash256 _hash;

        private void PrepareHash()
        {
            if (_hashBytes == null)
            {
                var rnd = new Random(42);
                _hashBytes = new byte[Hash256.SizeInBytes];
                rnd.NextBytes(_hashBytes);
                _hash = new Hash256(new ReadOnlySpan<byte>(_hashBytes, 0, Hash256.SizeInBytes));
            }
        }

        [Fact]
        public void HashConversions()
        {
            PrepareHash();

            var convertedHash = new byte[Hash256.SizeInBytes];
            _hash.WriteTo(new Span<byte>(convertedHash, 0, Hash256.SizeInBytes));
            //Console.WriteLine(BitConverter.ToString(hashBytes));
            //Console.WriteLine(BitConverter.ToString(convertedHash));
            Assert.Equal(_hashBytes, convertedHash);
        }

        [Fact]
        public void HashWriteException()
        {
            PrepareHash();
            var convertedHash = new byte[Hash256.SizeInBytes - 1];
            Assert.Throws<IndexOutOfRangeException>(() =>
                _hash.WriteTo(new Span<byte>(convertedHash, 0, Hash256.SizeInBytes - 1)));
        }

        [Fact]
        public void CompareTrue()
        {
            PrepareHash();

            var leftLeft = BigEndianConverter.ToUInt64(new Span<byte>(_hashBytes).Slice(0, 8));
            var leftRight = BigEndianConverter.ToUInt64(new Span<byte>(_hashBytes).Slice(8, 8));
            var rightLeft = BigEndianConverter.ToUInt64(new Span<byte>(_hashBytes).Slice(16, 8));
            var rightRight = BigEndianConverter.ToUInt64(new Span<byte>(_hashBytes).Slice(24, 8));

            Hash256 toCompare = new Hash256(leftLeft, leftRight, rightLeft, rightRight);

            Assert.Equal(0, _hash.CompareTo(toCompare));
        }

        [Fact]
        public void CompareFalse()
        {
            PrepareHash();

            var leftLeft = BigEndianConverter.ToUInt64(new Span<byte>(_hashBytes).Slice(0, 8));
            var leftRight = BigEndianConverter.ToUInt64(new Span<byte>(_hashBytes).Slice(8, 8));
            var rightLeft = BigEndianConverter.ToUInt64(new Span<byte>(_hashBytes).Slice(16, 8));
            var rightRight = BigEndianConverter.ToUInt64(new Span<byte>(_hashBytes).Slice(24, 8));

            Hash256 toCompare = new Hash256(leftLeft, leftRight, rightLeft, --rightRight);

            Assert.Equal(1, _hash.CompareTo(toCompare));
        }

        [Fact]
        public void EndiannessHash256()
        {
            // A sequence of bytes, all different, each line fills an entire ulong
            var bytes = new byte[]
            {
                0x11, 0x12, 0x13, 0x14, 0x15, 0x16, 0x17, 0x18,
                0x21, 0x22, 0x23, 0x24, 0x25, 0x26, 0x27, 0x28,
                0x31, 0x32, 0x33, 0x34, 0x35, 0x36, 0x37, 0x38,
                0x41, 0x42, 0x43, 0x44, 0x45, 0x46, 0x47, 0x48,
            };

            var hash = new Hash256(bytes);

            // Did the bytes move to the correct locations in the four ulongs ? 
            Assert.Equal(0x1112131415161718UL, hash.Var1);
            Assert.Equal(0x2122232425262728UL, hash.Var2);
            Assert.Equal(0x3132333435363738UL, hash.Var3);
            Assert.Equal(0x4142434445464748UL, hash.Var4);

            // Does ToString() display the correct overall hash, in hex ?
            Assert.Equal(
                "1112131415161718 2122232425262728 3132333435363738 4142434445464748",
                hash.ToString());

            // Does writing out the hash back to a byte array restore the right order ?
            var write = new byte[Hash256.SizeInBytes];
            hash.WriteTo(write);

            Assert.Equal(bytes, write);
        }
    }
}