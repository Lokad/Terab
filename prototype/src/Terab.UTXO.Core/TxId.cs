using System;
using Terab.UTXO.Core.Hash;

namespace Terab.UTXO.Core
{
    /// <summary>
    /// Identifier of a Bitcoin transaction.
    /// <see cref="https://github.com/Bitcoin-ABC/bitcoin-abc"/>
    /// </summary>
    public struct TxId : IComparable<TxId>, IEquatable<TxId>
    {
        public const int SizeInBytes = Hash256.SizeInBytes;

        private readonly Hash256 _hash;

        public TxId(Hash256 hash)
        {
            _hash = hash;
        }

        public TxId(ReadOnlySpan<byte> buffer)
        {
            _hash = new Hash256(buffer);
        }

        public int CompareTo(TxId other)
        {
            return _hash.CompareTo(other._hash);
        }

        public bool Equals(TxId other)
        {
            return _hash.Equals(other._hash);
        }

        /// <summary>
        /// This function returns a hash whose bits have been set
        /// to 0 apart from the n leftmost bits.
        /// <see cref="Hash256.Truncate"/>
        /// </summary>
        public Hash256 Truncate(int n)
        {
            return _hash.Truncate(n);
        }

        /// <summary>
        /// Interprets the wrapped hash as a large unsigned integer, and
        /// returns the largest 'n' bits as an integer.
        /// <see cref="Hash256.Top"/>
        /// </summary>
        public int Top(int n)
        {
            return _hash.Top(n);
        }

        public void WriteTo(Span<byte> buffer)
        {
            _hash.WriteTo(buffer);
        }
    }
}
