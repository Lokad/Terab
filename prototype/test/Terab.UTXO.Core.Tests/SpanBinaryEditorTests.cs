using System;
using System.IO;
using Terab.UTXO.Core.Helpers;
using Xunit;

namespace Terab.UTXO.Core.Tests
{
    public class SpanBinaryEditorTests
    {
        private static readonly byte[] buffer = new byte[32];
        private static readonly MemoryStream stream;
        private static readonly BinaryReader binary;

        private static readonly Random random = new Random();

        static SpanBinaryEditorTests()
        {
            stream = new MemoryStream(buffer);
            binary = new BinaryReader(stream);
        }

        [Fact]
        public void TestMixOfAllTypes()
        {
            // we need a buffer that is larger than the 32 byte one that is
            // used for the round trip tests:
            byte[] bigBuffer = new byte[1024];
            var spe = new SpanBinaryEditor(bigBuffer);
            spe.Seek(16, SeekOrigin.Begin);
            Assert.Equal(16, spe.Position);

            int int32 = GetRandom<int>();
            bool boolean = GetRandom<bool>();
            byte octet = GetRandom<byte>();
            short int16 = GetRandom<short>();
            long int64 = GetRandom<long>();
            long int64be = GetRandom<long>();
            ushort uint16 = GetRandom<ushort>();
            uint uint32 = GetRandom<uint>();
            ulong uint64 = GetRandom<ulong>();
            ulong uint64be = GetRandom<ulong>();

            spe.WriteInt32(int32);
            spe.WriteBoolean(boolean);
            spe.WriteByte(octet);
            spe.WriteInt16(int16);
            spe.WriteInt64(int64);
            spe.WriteInt64BE(int64be);
            spe.WriteUInt16(uint16);
            spe.WriteUInt32(uint32);
            spe.WriteUInt64(uint64);
            spe.WriteUInt64BE(uint64be);

            spe.Seek(-20, SeekOrigin.Current);

            Assert.Equal(uint32, spe.ReadUInt32());
            Assert.Equal(uint64, spe.ReadUInt64());
            Assert.Equal(uint64be, spe.ReadUInt64BE());

            spe.Seek(-spe.Position, SeekOrigin.Current);
            Assert.Equal(0, spe.Position);

            spe.Seek(16, SeekOrigin.Current);
            Assert.Equal(16, spe.Position);
            Assert.Equal(int32, spe.ReadInt32());
            if (boolean)
            { // same special handling of boolean as in RoundTripBoolean
                Assert.True(spe.ReadBoolean());
            }
            else
            {
                Assert.False(spe.ReadBoolean());
            }
            Assert.Equal(octet, spe.ReadByte());
            Assert.Equal(int16, spe.ReadInt16());
            Assert.Equal(int64, spe.ReadInt64());
            Assert.Equal(int64be, spe.ReadInt64BE());
            Assert.Equal(uint16, spe.ReadUInt16());
        }

        [Fact]
        public void RoundTripInt64()
        {
            Check(0);
            Check(1);
            Check(-1);
            Check(10);
            Check(-10);
            Check(sbyte.MaxValue);
            Check(sbyte.MinValue);
            Check(byte.MaxValue);
            Check(short.MaxValue);
            Check(short.MinValue);
            Check(ushort.MaxValue);
            Check(int.MaxValue);
            Check(int.MinValue);
            Check(uint.MaxValue);
            Check(long.MaxValue);
            Check(long.MinValue);
            for (int i = 0; i < 15; ++i)
            {
                Check(GetRandom<long>());
            }

            void Check(Int64 toWrite)
            {
                Array.Clear(buffer, 0, buffer.Length);
                var sbe = new SpanBinaryEditor(buffer);
                sbe.WriteInt64(toWrite);
                sbe.Seek(0, SeekOrigin.Begin);
                Int64 actual = sbe.ReadInt64();
                CheckEquality(toWrite, actual);

                stream.Seek(0, SeekOrigin.Begin);
                CheckEquality(toWrite, binary.ReadInt64());
            }
        }

        [Fact]
        public void RoundTripInt64BigEndian()
        {
            Check(0);
            Check(1);
            Check(-1);
            Check(10);
            Check(-10);
            // the largest possible set of extremal values that fits a CLR
            // signed integer type:
            Check(sbyte.MaxValue);
            Check(sbyte.MinValue);
            Check(byte.MaxValue);
            Check(short.MaxValue);
            Check(short.MinValue);
            Check(ushort.MaxValue);
            Check(int.MaxValue);
            Check(int.MinValue);
            Check(uint.MaxValue);
            Check(long.MaxValue);
            Check(long.MinValue);
            for (int i = 0; i < 15; ++i)
            {
                Check(GetRandom<long>());
            }

            void Check(Int64 toWrite)
            {
                Array.Clear(buffer, 0, buffer.Length);
                var sbe = new SpanBinaryEditor(buffer);
                sbe.WriteInt64BE(toWrite);
                sbe.Seek(0, SeekOrigin.Begin);
                Int64 actual = sbe.ReadInt64BE();
                CheckEquality(toWrite, actual);
            }
        }

        [Fact]
        public void RoundTripInt32()
        {
            Check(0);
            Check(1);
            Check(-1);
            Check(10);
            Check(-10);
            Check(sbyte.MaxValue);
            Check(sbyte.MinValue);
            Check(byte.MaxValue);
            Check(short.MaxValue);
            Check(short.MinValue);
            Check(ushort.MaxValue);
            Check(int.MaxValue);
            Check(int.MinValue);
            // those would fit in an Int64, but don't fit in an Int32:
            //CheckRoundTripInt32(uint.MaxValue);
            //CheckRoundTripInt32(long.MaxValue);
            //CheckRoundTripInt32(long.MinValue);
            for (int i = 0; i < 15; ++i)
            {
                Check(GetRandom<int>());
            }

            void Check(Int32 toWrite)
            {
                Array.Clear(buffer, 0, buffer.Length);
                var sbe = new SpanBinaryEditor(buffer);
                sbe.WriteInt32(toWrite);
                sbe.Seek(0, SeekOrigin.Begin);
                Int32 actual = sbe.ReadInt32();
                CheckEquality(toWrite, actual);

                stream.Seek(0, SeekOrigin.Begin);
                CheckEquality(toWrite, binary.ReadInt32());
            }
        }

        [Fact]
        public void RoundTripInt16()
        {
            Check(0);
            Check(1);
            Check(-1);
            Check(10);
            Check(-10);
            Check(sbyte.MaxValue);
            Check(sbyte.MinValue);
            Check(byte.MaxValue);
            Check(short.MaxValue);
            Check(short.MinValue);
            // those would fit in an Int32, but don't fit in an Int16:
            //CheckRoundTripInt16(ushort.MaxValue);
            //CheckRoundTripInt16(int.MaxValue);
            //CheckRoundTripInt16(int.MinValue);
            for (int i = 0; i < 15; ++i)
            {
                Check(GetRandom<short>());
            }

            void Check(Int16 toWrite)
            {
                Array.Clear(buffer, 0, buffer.Length);
                var sbe = new SpanBinaryEditor(buffer);
                sbe.WriteInt16(toWrite);
                sbe.Seek(0, SeekOrigin.Begin);
                Int16 actual = sbe.ReadInt16();
                CheckEquality(toWrite, actual);

                stream.Seek(0, SeekOrigin.Begin);
                CheckEquality(toWrite, binary.ReadInt16());
            }
        }

        [Fact]
        public void RoundTripSByte()
        {
            Check(0);
            Check(1);
            Check(-1);
            Check(10);
            Check(-10);
            Check(sbyte.MaxValue);
            Check(sbyte.MinValue);
            // those would fit in an Int16, but don't fit in an SByte:
            //Check(byte.MaxValue);
            //Check(short.MaxValue);
            //Check(short.MinValue);
            for (int i = 0; i < 15; ++i)
            {
                Check(GetRandom<sbyte>());
            }

            void Check(SByte toWrite)
            {
                Array.Clear(buffer, 0, buffer.Length);
                var sbe = new SpanBinaryEditor(buffer);
                sbe.WriteSbyte(toWrite);
                sbe.Seek(0, SeekOrigin.Begin);
                SByte actual = sbe.ReadSbyte();
                CheckEquality(toWrite, actual);

                stream.Seek(0, SeekOrigin.Begin);
                CheckEquality(toWrite, binary.ReadSByte());
            }
        }

        [Fact]
        public void RoundTripUInt64()
        {
            Check(0);
            Check(1);
            Check(10);
            Check((ulong) sbyte.MaxValue);
            Check(byte.MaxValue);
            Check((ulong) short.MaxValue);
            Check(ushort.MaxValue);
            Check(int.MaxValue);
            Check(uint.MaxValue);
            Check(long.MaxValue);
            Check(ulong.MaxValue);
            for (int i = 0; i < 15; ++i)
            {
                Check(GetRandom<ulong>());
            }

            void Check(UInt64 toWrite)
            {
                Array.Clear(buffer, 0, buffer.Length);
                var sbe = new SpanBinaryEditor(buffer);
                sbe.WriteUInt64(toWrite);
                sbe.Seek(0, SeekOrigin.Begin);
                UInt64 actual = sbe.ReadUInt64();
                CheckEquality(toWrite, actual);

                stream.Seek(0, SeekOrigin.Begin);
                CheckEquality(toWrite, binary.ReadUInt64());
            }
        }

        [Fact]
        public void RoundTripUInt64BE()
        {
            Check(0);
            Check(1);
            Check(10);
            Check((ulong) sbyte.MaxValue);
            Check(byte.MaxValue);
            Check((ulong) short.MaxValue);
            Check(ushort.MaxValue);
            Check(int.MaxValue);
            Check(uint.MaxValue);
            Check(long.MaxValue);
            Check(ulong.MaxValue);
            for (int i = 0; i < 15; ++i)
            {
                Check(GetRandom<ulong>());
            }

            void Check(UInt64 toWrite)
            {
                Array.Clear(buffer, 0, buffer.Length);
                var sbe = new SpanBinaryEditor(buffer);
                sbe.WriteUInt64BE(toWrite);
                sbe.Seek(0, SeekOrigin.Begin);
                UInt64 actual = sbe.ReadUInt64BE();
                CheckEquality(toWrite, actual);
            }
        }

        [Fact]
        public void RoundTripUInt32()
        {
            Check(0);
            Check(1);
            Check(10);
            Check((uint) sbyte.MaxValue);
            Check(byte.MaxValue);
            Check((uint) short.MaxValue);
            Check(ushort.MaxValue);
            Check(int.MaxValue);
            Check(uint.MaxValue);
            //CheckRoundTripUInt32(long.MaxValue);
            //CheckRoundTripUInt32(ulong.MaxValue);
            for (int i = 0; i < 15; ++i)
            {
                Check(GetRandom<uint>());
            }

            void Check(UInt32 toWrite)
            {
                Array.Clear(buffer, 0, buffer.Length);
                var sbe = new SpanBinaryEditor(buffer);
                sbe.WriteUInt32(toWrite);
                sbe.Seek(0, SeekOrigin.Begin);
                UInt32 actual = sbe.ReadUInt32();
                CheckEquality(toWrite, actual);

                stream.Seek(0, SeekOrigin.Begin);
                CheckEquality(toWrite, binary.ReadUInt32());
            }
        }

        [Fact]
        public void RoundTripUInt16()
        {
            Check(0);
            Check(1);
            Check(10);
            Check((ushort) sbyte.MaxValue);
            Check(byte.MaxValue);
            Check((ushort) short.MaxValue);
            Check(ushort.MaxValue);
            //CheckRoundTripUInt16(int.MaxValue);
            //CheckRoundTripUInt16(uint.MaxValue);
            for (int i = 0; i < 15; ++i)
            {
                Check(GetRandom<ushort>());
            }

            void Check(UInt16 toWrite)
            {
                Array.Clear(buffer, 0, buffer.Length);
                var sbe = new SpanBinaryEditor(buffer);
                sbe.WriteUInt16(toWrite);
                sbe.Seek(0, SeekOrigin.Begin);
                UInt16 actual = sbe.ReadUInt16();
                CheckEquality(toWrite, actual);

                stream.Seek(0, SeekOrigin.Begin);
                CheckEquality(toWrite, binary.ReadUInt16());
            }
        }

        [Fact]
        public void RoundTripByte()
        {
            Check(0);
            Check(1);
            Check(10);
            Check((byte) sbyte.MaxValue);
            Check(byte.MaxValue);
            //CheckRoundTripByte((ushort)short.MaxValue);
            //CheckRoundTripByte(ushort.MaxValue);
            for (int i = 0; i < 15; ++i)
            {
                Check(GetRandom<byte>());
            }

            void Check(Byte toWrite)
            {
                Array.Clear(buffer, 0, buffer.Length);
                var sbe = new SpanBinaryEditor(buffer);
                sbe.WriteByte(toWrite);
                sbe.Seek(0, SeekOrigin.Begin);
                Byte actual = sbe.ReadByte();
                CheckEquality(toWrite, actual);

                stream.Seek(0, SeekOrigin.Begin);
                CheckEquality(toWrite, binary.ReadByte());
            }
        }

        [Fact]
        public void TestBoolean()
        {
            Check(true);
            Check(false);
            for (int i = 0; i < 15; ++i)
            {
                Check(GetRandom<bool>());
            }

            void Check(Boolean toWrite)
            {
                Array.Clear(buffer, 0, buffer.Length);
                var sbe = new SpanBinaryEditor(buffer);
                sbe.WriteBoolean(toWrite);
                sbe.Seek(0, SeekOrigin.Begin);
                Boolean actual = sbe.ReadBoolean();
                // this is a slightly different implementation, because
                // (Read|Write)Boolean do not preserve the exact bit pattern
                // of the passed in boolean. Instead, it normalizes them to 
                // 1/0 when writing
                if (toWrite)
                {
                    Assert.True(actual);
                }
                else
                {
                    Assert.False(actual);
                }
            }
        }

        public T GetRandom<T>() // where T : unmanaged
        {
            // Resharper does not yet like the above generic constraint
            var result = new T[1];
            var n = Buffer.ByteLength(result);
            var randomBytes = new byte[n];
            random.NextBytes(randomBytes);
            for (int i = 0; i < n; ++i)
            {
                Buffer.SetByte(result, i, randomBytes[i]);
            }

            return result[0];
        }

        public void CheckEquality<T>(T expected, T actual) where T : IEquatable<T>
        {
            Assert.True(expected.Equals(actual), $"Expected: {expected}, Actual: {actual}; Seed value");
        }
    }
}