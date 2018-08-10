using System;
using Terab.UTXO.Core.Hash;
using Xunit;

namespace Terab.UTXO.Core.Tests
{
    public class Hash256ExtensionsTests
    {
        [Fact]
        public void TestTrunking256()
        {
            ulong LeftLeft = ulong.MaxValue;
            ulong LeftRight = ulong.MaxValue;
            ulong RightLeft = ulong.MaxValue;
            ulong RightRight = ulong.MaxValue;

            var baseHash = new Hash256(LeftLeft, LeftRight, RightLeft, RightRight);
            for (int i = 0; i < sizeof(ulong) * 8; i++)
            {
                var toLeave = 64 * 4 - i;
                Console.WriteLine("To leave: " + toLeave);
                var trunkatedHash = baseHash.Truncate(toLeave);
                var expectedHash = new Hash256(LeftLeft, LeftRight, RightLeft, RightRight << i);
                Console.WriteLine(expectedHash);
                Console.WriteLine(trunkatedHash);
                Assert.Equal(expectedHash, trunkatedHash);
            }

            for (int i = 64; i < 64 + sizeof(ulong) * 8; i++)
            {
                var toLeave = 64 * 4 - i;
                Console.WriteLine("To leave: " + toLeave);
                var trunkatedHash = baseHash.Truncate(toLeave);
                var expectedHash = new Hash256(LeftLeft, LeftRight, RightLeft << i % 64);
                Console.WriteLine(expectedHash);
                Console.WriteLine(trunkatedHash);
                Assert.Equal(expectedHash, trunkatedHash);
            }

            for (int i = 128; i < 128 + sizeof(ulong) * 8; i++)
            {
                var toLeave = 64 * 4 - i;
                Console.WriteLine("To leave: " + toLeave);
                var trunkatedHash = baseHash.Truncate(toLeave);
                var expectedHash = new Hash256(LeftLeft, LeftRight << i % 64);
                Console.WriteLine(expectedHash);
                Console.WriteLine(trunkatedHash);
                Assert.Equal(expectedHash, trunkatedHash);
            }

            for (int i = 192; i < 192 + sizeof(ulong) * 8; i++)
            {
                var toLeave = 64 * 4 - i;
                Console.WriteLine("To leave: " + toLeave);
                var trunkatedHash = baseHash.Truncate(toLeave);
                var expectedHash = new Hash256(LeftLeft << i % 64);
                Console.WriteLine(expectedHash);
                Console.WriteLine(trunkatedHash);
                Assert.Equal(expectedHash, trunkatedHash);
            }
            Console.WriteLine("To leave: 0");
            var zeroHash = Hash256.Zero;
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
                0x21, 0x22, 0x23, 0x24, 0x25, 0x26, 0x27, 0x28,
                0x31, 0x32, 0x33, 0x34, 0x35, 0x36, 0x37, 0x38,
                0x41, 0x42, 0x43, 0x44, 0x45, 0x46, 0x47, 0x48
            };

            var hash = new Hash256(bytes);
            var truncatedHash = hash.Truncate(32);

            // Does ToString() display the correct overall hash, in hex ?
            Assert.Equal(
                "1112131400000000 0000000000000000 0000000000000000 0000000000000000",
                truncatedHash.ToString());

            // Did the bytes move to the correct locations in the four ulongs ? 
            Assert.Equal(0x1112131400000000UL, truncatedHash.Var1);
            Assert.Equal(0x0000000000000000UL, truncatedHash.Var2);
            Assert.Equal(0x0000000000000000UL, truncatedHash.Var3);
            Assert.Equal(0x0000000000000000UL, truncatedHash.Var4);               
        }

        [Fact]
        public void EndiannessWithTruncation2()
        {
            // A sequence of bytes, all different, each line fills an entire ulong
            var bytes = new byte[]
            {
                0x1F, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,
                0x2F, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,
                0x3F, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,
                0x4F, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF
            };

            var hash = new Hash256(bytes);
            var truncatedHash = hash.Truncate(238);

            // Does ToString() display the correct overall hash, in hex ?
            Assert.Equal(
                "1FFFFFFFFFFFFFFF 2FFFFFFFFFFFFFFF 3FFFFFFFFFFFFFFF 4FFFFFFFFFFC0000",
                truncatedHash.ToString());

            // Did the bytes move to the correct locations in the four ulongs ? 
            Assert.Equal(0x1FFFFFFFFFFFFFFFUL, truncatedHash.Var1);
            Assert.Equal(0x2FFFFFFFFFFFFFFFUL, truncatedHash.Var2);
            Assert.Equal(0x3FFFFFFFFFFFFFFFUL, truncatedHash.Var3);
            Assert.Equal(0x4FFFFFFFFFFC0000UL, truncatedHash.Var4);
        }

        [Fact]
        public void EndiannessWithTruncation3()
        {
            // A sequence of bytes, all different, each line fills an entire ulong
            var bytes = new byte[]
            {
                0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,
                0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,
                0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,
                0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF
            };

            var hash = new Hash256(bytes);
            var truncatedHash = hash.Truncate(149);

            // Does ToString() display the correct overall hash, in hex ?
            Assert.Equal(
                "FFFFFFFFFFFFFFFF FFFFFFFFFFFFFFFF FFFFF80000000000 0000000000000000",
                truncatedHash.ToString());

            // Did the bytes move to the correct locations in the four ulongs ? 
            Assert.Equal(0xFFFFFFFFFFFFFFFFUL, truncatedHash.Var1);
            Assert.Equal(0xFFFFFFFFFFFFFFFFUL, truncatedHash.Var2);
            Assert.Equal(0xFFFFF80000000000UL, truncatedHash.Var3);
            Assert.Equal(0x0000000000000000UL, truncatedHash.Var4);
        }
    }
}
