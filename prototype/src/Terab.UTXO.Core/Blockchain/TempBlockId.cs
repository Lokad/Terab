using System;
using System.IO;
using System.Security.Cryptography;
using Terab.UTXO.Core.Hash;
using Terab.UTXO.Core.Helpers;

namespace Terab.UTXO.Core.Blockchain
{
    /// <summary>
    /// Identifier of a temporary(uncommited) blockId
    /// <see cref="UncommittedBlock"/>
    /// </summary>
    public struct TempBlockId : IComparable<TempBlockId>, IEquatable<TempBlockId>
    {
        public const int SizeInBytes = Hash256.SizeInBytes;

        private readonly Hash128 _hash;

        public TempBlockId(Hash128 hash)
        {
            _hash = hash;
        }

        public static TempBlockId Create(ref SpanBinaryReader buffer)
        {
            return new TempBlockId(new Hash128(ref buffer));
        }

        public int CompareTo(TempBlockId other)
        {
            return _hash.CompareTo(other._hash);
        }

        public bool Equals(TempBlockId other)
        {
            return _hash.Equals(other._hash);
        }

        /// <summary>
        /// Generate an unique TempBlockId.
        /// This method is based on <see cref="RNGCryptoServiceProvider"/>.
        /// </summary>
        public static TempBlockId Create()
        {
            return new TempBlockId(Hash128.CreateUniqueIdentifier());
        }

        public void WriteTo(ref SpanBinaryEditor buffer)
        {
            if (buffer.RemainingLength < SizeInBytes)
            {
                throw new IndexOutOfRangeException(
                    $"Span length {buffer.Length} is too short. It should be at least {SizeInBytes} bytes long.");
            }

            BigEndianConverter.WriteBytes(ref buffer, _hash.Left);
            BigEndianConverter.WriteBytes(ref buffer, _hash.Right);
        }

        // TODO : Possible merge with WriteTo taking SpanBinaryEditor
        public void WriteTo(BinaryWriter writer)
        {
            writer.WriteHash128(_hash);
        }
    }
}