using System;
using Terab.UTXO.Core.Hash;
using Xunit;

namespace Terab.UTXO.Core.Tests
{
    public class Hash128ExtensionsTests
    {
        [Fact]
        public void TestTrunking128()
        {
            ulong Left = ulong.MaxValue;
            ulong Right = ulong.MaxValue;

            var baseHash = new Hash128(Left, Right);
            for (int i = 0; i < sizeof(ulong) * 8; i++)
            {
                var toLeave = 64 * 2 - i;
                Console.WriteLine("To leave: " + toLeave);
                var trunkatedHash = baseHash.Truncate(toLeave);
                var expectedHash = new Hash128(Left, Right << i);
                Console.WriteLine(expectedHash);
                Console.WriteLine(trunkatedHash);
                Assert.Equal(expectedHash, trunkatedHash);
            }

            for (int i = 64; i < 64 + sizeof(ulong) * 8; i++)
            {
                var toLeave = 64 * 2 - i;
                Console.WriteLine("To leave: " + toLeave);
                var trunkatedHash = baseHash.Truncate(toLeave);
                var expectedHash = new Hash128(Left << i % 64);
                Console.WriteLine(expectedHash);
                Console.WriteLine(trunkatedHash);
                Assert.Equal(expectedHash, trunkatedHash);
            }

            Console.WriteLine("To leave: 0");
            var zeroHash = new Hash128(0);
            Console.WriteLine(zeroHash);
            Console.WriteLine(baseHash.Truncate(0));
            Assert.Equal(zeroHash, baseHash.Truncate(0));
        }

        [Fact]
        public void EndiannessWithTruncation1()
        {
            // A sequence of bytes, all different, each line fills an entire ulong
            var bytes = new byte[]
            {
                0x11, 0x12, 0x13, 0x14, 0x15, 0x16, 0x17, 0x18,
                0x21, 0x22, 0x23, 0x24, 0x25, 0x26, 0x27, 0x28
            };

            var hash = new Hash128(bytes);
            var truncatedHash = hash.Truncate(32);

            // Does ToString() display the correct overall hash, in hex ?
            Assert.Equal(
                "11121314000000000000000000000000",
                truncatedHash.ToString());

            // Did the bytes move to the correct locations in the four ulongs ? 
            Assert.Equal(0x1112131400000000UL, truncatedHash.Left);
            Assert.Equal(0x0000000000000000UL, truncatedHash.Right);
        }

        [Fact]
        public void EndiannessWithTruncation2()
        {
            // A sequence of bytes, all different, each line fills an entire ulong
            var bytes = new byte[]
            {
                0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,
                0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF
            };

            var hash = new Hash128(bytes);
            var truncatedHash = hash.Truncate(110);

            // Does ToString() display the correct overall hash, in hex ?
            Assert.Equal(
                "FFFFFFFFFFFFFFFFFFFFFFFFFFFC0000",
                truncatedHash.ToString());

            // Did the bytes move to the correct locations in the four ulongs ? 
            Assert.Equal(0xFFFFFFFFFFFFFFFFUL, truncatedHash.Left);
            Assert.Equal(0xFFFFFFFFFFFC0000UL, truncatedHash.Right);
        }

        [Fact]
        public void EndiannessWithTruncation3()
        {
            // A sequence of bytes, all different, each line fills an entire ulong
            var bytes = new byte[]
            {
                0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,
                0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF
            };

            var hash = new Hash128(bytes);
            var truncatedHash = hash.Truncate(21);

            // Does ToString() display the correct overall hash, in hex ?
            Assert.Equal(
                "FFFFF800000000000000000000000000",
                truncatedHash.ToString());

            // Did the bytes move to the correct locations in the four ulongs ? 
            Assert.Equal(0xFFFFF80000000000UL, truncatedHash.Left);
            Assert.Equal(0x0000000000000000UL, truncatedHash.Right);
        }
    }
}
