using System;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using Terab.UTXO.Core.Helpers;

namespace Terab.UTXO.Core.Hash
{
    public struct Hash256 : IComparable<Hash256>, IEquatable<Hash256>
    {
        public static readonly Hash256 Zero = new Hash256(0UL);

        public const int SizeInBytes = 32;

        // Leftest of the 4 parts
        public ulong Var1 { get; }

        // Second leftest of the 4 parts
        public ulong Var2 { get; }

        // Second rightest of the 4 parts
        public ulong Var3 { get; }

        // Rightest of the 4 parts
        public ulong Var4 { get; }

        public Hash256(ulong var1, ulong var2 = 0UL, ulong var3 = 0UL, ulong var4 = 0UL)
        {
            Var1 = var1;
            Var2 = var2;
            Var3 = var3;
            Var4 = var4;
        }

        /// <summary>
        /// Create a hash256 from bytes given in a span.
        /// </summary>
        public Hash256(ReadOnlySpan<byte> buffer)
        {
            if (buffer.Length < SizeInBytes)
            {
                throw new IndexOutOfRangeException(
                    $"The size of the buffer needs to be at least {SizeInBytes} but is {buffer.Length}");
            }

            Var1 = BigEndianConverter.ToUInt64(buffer.Slice(0, 8));
            Var2 = BigEndianConverter.ToUInt64(buffer.Slice(8, 8));
            Var3 = BigEndianConverter.ToUInt64(buffer.Slice(16, 8));
            Var4 = BigEndianConverter.ToUInt64(buffer.Slice(24, 8));
        }

        public Hash256(ref SpanBinaryReader buffer)
        {
            if (buffer.RemainingLength < SizeInBytes)
            {
                throw new IndexOutOfRangeException(
                    $"The size of the buffer needs to be at least {SizeInBytes} but is {buffer.Length}");
            }

            Var1 = BigEndianConverter.ReadUInt64(ref buffer);
            Var2 = BigEndianConverter.ReadUInt64(ref buffer);
            Var3 = BigEndianConverter.ReadUInt64(ref buffer);
            Var4 = BigEndianConverter.ReadUInt64(ref buffer);
        }

        /// <see cref="IComparable{T}.CompareTo"/>
        [Pure]
        public int CompareTo(Hash256 other)
        {
            var leftLeftComparison = Var1.CompareTo(other.Var1);
            if (leftLeftComparison != 0) return leftLeftComparison;
            var leftRightComparison = Var2.CompareTo(other.Var2);
            if (leftRightComparison != 0) return leftRightComparison;
            var rightLeftComparison = Var3.CompareTo(other.Var3);
            if (rightLeftComparison != 0) return rightLeftComparison;
            return Var4.CompareTo(other.Var4);
        }


        public override bool Equals(object obj)
        {
            return obj is Hash256 hash256 && this == hash256;
        }

        public override int GetHashCode()
        {
            throw new NotSupportedException();
        }

        public static bool operator ==(Hash256 ls, Hash256 rs)
        {
            return ls.Var1 == rs.Var1
                   && ls.Var2 == rs.Var2
                   && ls.Var3 == rs.Var3
                   && ls.Var4 == rs.Var4;
        }

        public static bool operator !=(Hash256 ls, Hash256 rs)
        {
            return !(ls == rs);
        }

        /// <summary>
        /// Interprets the hash as a large unsigned integer, and
        /// returns the largest 'n' bits as an integer.
        /// </summary>
        /// <param name="n">number of bits as mentioned in summary</param>
        /// <returns></returns>
        [Pure]
        public int Top(int n)
        {
            Debug.Assert(n > 0 && n < 64);
            return (int) (Var1 >> -n);
        }

        public override string ToString() =>
            $"{Var1:X16} {Var2:X16} {Var3:X16} {Var4:X16}";

        [Pure]
        public bool Equals(Hash256 otherHash)
        {
            return this == otherHash;
        }

        /// <summary>
        /// Serialize the hash256 into a given Span.
        /// </summary>
        public void WriteTo(Span<byte> buffer)
        {
            if (buffer.Length < SizeInBytes)
            {
                throw new IndexOutOfRangeException(
                    $"Span length {buffer.Length} is too short. It should be at least {SizeInBytes} bytes long.");
            }

            BigEndianConverter.TryWriteBytes(buffer.Slice(0, 8), Var1);
            BigEndianConverter.TryWriteBytes(buffer.Slice(8, 8), Var2);
            BigEndianConverter.TryWriteBytes(buffer.Slice(16, 8), Var3);
            BigEndianConverter.TryWriteBytes(buffer.Slice(24, 8), Var4);
        }

        /// <summary>
        /// This function returns a hash whose bits have been set
        /// to 0 apart from the n leftmost bits.
        /// </summary>
        /// <param name="n">
        /// The number of bits that are not supposed to be zeroed.
        /// </param>
        [Pure]
        public Hash256 Truncate(int n)
        {
            if (n >= 256)
                return this;
            if (n <= 0)
                return new Hash256(0UL);
            if (n > 192)
            {
                return new Hash256(Var1, Var2, Var3, Var4 & (~0UL << -n));
            }
            if (n == 192)
            {
                return new Hash256(Var1, Var2, Var3);
            }
            if (n > 128)
            {
                return new Hash256(Var1, Var2, Var3 & (~0UL << -n));
            }
            if (n == 128)
            {
                return new Hash256(Var1, Var2);
            }
            if (n > 64)
            {
                return new Hash256(Var1, Var2 & (~0UL << -n));
            }
            if (n == 64)
            {
                return new Hash256(Var1);
            }
            return new Hash256(Var1 & (~0UL << -n));
        }
    }
}