using System;
using System.Diagnostics.Contracts;
using System.Security.Cryptography;
using Terab.UTXO.Core.Helpers;

namespace Terab.UTXO.Core.Hash
{
    public struct Hash128 : IComparable<Hash128>, IEquatable<Hash128>
    {
        public const int SizeInBytes = 16;

        public ulong Left { get; }

        public ulong Right { get; }


        public Hash128(ulong left, ulong right = 0UL)
        {
            Left = left;
            Right = right;
        }

        /// <summary>
        /// This method writes the hash into a given buffer.
        /// </summary>
        /// <exception cref="IndexOutOfRangeException">
        /// Exception thrown only if the buffer is too short. If the buffer is
        /// too long, only the first elements are filled, but the program will
        /// not raise a concern.
        /// </exception>
        /// <param name="buffer"></param>
        public Hash128(ReadOnlySpan<byte> buffer)
        {
            if (buffer.Length < SizeInBytes)
            {
                throw new IndexOutOfRangeException(
                    $"The size of the buffer needs to be at least {SizeInBytes} but is {buffer.Length}");
            }

            Left = BigEndianConverter.ToUInt64(buffer.Slice(0, 8));
            Right = BigEndianConverter.ToUInt64(buffer.Slice(8));
        }

        public Hash128(ref SpanBinaryReader buffer)
        {
            if (buffer.Length < SizeInBytes)
            {
                throw new IndexOutOfRangeException(
                    $"The size of the buffer needs to be at least {SizeInBytes} but is {buffer.Length}");
            }

            Left = BigEndianConverter.ReadUInt64(ref buffer);
            Right = BigEndianConverter.ReadUInt64(ref buffer);
        }

        [Pure]
        public int CompareTo(Hash128 other)
        {
            var leftComparison = Left.CompareTo(other.Left);
            if (leftComparison != 0) return leftComparison;
            return Right.CompareTo(other.Right);
        }

        public override bool Equals(object obj)
        {
            return obj is Hash128 hash128 && this == hash128;
        }

        public override int GetHashCode()
        {
            throw new NotSupportedException();
        }

        public static bool operator ==(Hash128 ls, Hash128 rs)
        {
            return ls.Left == rs.Left && ls.Right == rs.Right;
        }

        public static bool operator !=(Hash128 ls, Hash128 rs)
        {
            return !(ls == rs);
        }

        [Pure]
        public bool Equals(Hash128 otherHash)
        {
            return this == otherHash;
        }

        public Hash128 Truncate(int n)
        {
            if (n >= 128)
                return this;
            if (n <= 0)
                return new Hash128(0UL);
            if (n > 64)
            {
                return new Hash128(Left, Right & (~0UL << -n));
            }
            return new Hash128(Left & (~0UL << -n));
        }

        public void WriteTo(Span<byte> buffer)
        {
            if (buffer.Length < SizeInBytes)
            {
                throw new IndexOutOfRangeException(
                    $"Span length {buffer.Length} is too short. It should be at least {SizeInBytes} bytes long.");
            }

            BigEndianConverter.TryWriteBytes(buffer.Slice(0, 8), Left);
            BigEndianConverter.TryWriteBytes(buffer.Slice(8), Right);
        }


        public override string ToString() => $"{Left:X16}{Right:X16}";

        // TODO: [vermorel] Comment should clarify the intent.

        public static Hash128 CreateUniqueIdentifier()
        {
            var buffer = new byte[8];
            var rng = new RNGCryptoServiceProvider();

            rng.GetBytes(buffer);
            var rand1 = BitConverter.ToUInt64(buffer, 0);

            rng.GetBytes(buffer);
            var rand2 = BitConverter.ToUInt64(buffer, 0);

            return new Hash128(rand1, rand2);
        }
    }
}