using System;
using System.Dynamic;
using System.IO;
using Terab.UTXO.Core.Hash;
using Terab.UTXO.Core.Helpers;

namespace Terab.UTXO.Core.Blockchain
{
    /// <summary>
    /// Identifier of a Bitcoin block.
    /// </summary>
    public struct BlockId : IComparable<BlockId>, IEquatable<BlockId>
    {
        public const int SizeInBytes = Hash256.SizeInBytes;

        /// <summary>
        /// Hash of the first block of the blockchain, as documented at
        /// https://en.bitcoin.it/wiki/Genesis_block.
        /// </summary>
        private static readonly Hash256 GenesisHash =
            new Hash256(0x000000000019d668UL, 0x9c085ae165831e93UL, 0x4ff763ae46a2a6c1UL, 0x72b3f1b60a8ce26fUL);

        public static readonly BlockId Genesis = new BlockId(GenesisHash);

        private readonly Hash256 _hash;

        public BlockId(Hash256 hash)
        {
            _hash = hash;
        }

        public static BlockId Create(ref SpanBinaryReader buffer)
        {
            return new BlockId(new Hash256(ref buffer));
        }

        public int CompareTo(BlockId other)
        {
            return _hash.CompareTo(other._hash);
        }

        public bool Equals(BlockId other)
        {
            return _hash.Equals(other._hash);
        }

        public void WriteTo(ref SpanBinaryEditor buffer)
        {
            if (buffer.Length < SizeInBytes)
            {
                throw new IndexOutOfRangeException(
                    $"Span length {buffer.Length} is too short. It should be at least {SizeInBytes} bytes long.");
            }

            BigEndianConverter.WriteBytes(ref buffer, _hash.Var1);
            BigEndianConverter.WriteBytes(ref buffer, _hash.Var2);
            BigEndianConverter.WriteBytes(ref buffer, _hash.Var3);
            BigEndianConverter.WriteBytes(ref buffer, _hash.Var4);
        }

        // TODO : Possible merge with WriteTo taking SpanBinaryEditor
        public void WriteTo(BinaryWriter writer)
        {
            writer.WriteHash256(_hash);
        }
    }
}