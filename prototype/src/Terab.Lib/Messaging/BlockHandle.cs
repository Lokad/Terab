// Copyright Lokad 2018 under MIT BCH.
using System;
using Terab.Lib.Chains;

namespace Terab.Lib.Messaging
{
    /// <summary>
    /// Opaque handle to a block of Terab server (zero means 'undefined' though).
    /// </summary>
    /// <remarks>
    /// Terab client communicates with Terab server based on  this opaque struct.
    /// Inside Terab server, it maps to a <see cref="BlockAlias"/>.
    /// </remarks>
    public struct BlockHandle : IEquatable<BlockHandle>
    {
        public const uint DefaultMask = 0xF1E2D3C4U;

        public const int SizeInBytes = sizeof(int);

        public static BlockHandle Undefined = new BlockHandle(0x00);

        private readonly uint _value;

        public uint Value => _value;

        public bool IsDefined => (_value & 0x01) != 0;

        public BlockHandle(uint value)
        {
            _value = value;
        }

        public bool Equals(BlockHandle other)
        {
            return other._value == _value;
        }

        public override int GetHashCode()
        {
            return _value.GetHashCode();
        }

        public static bool operator ==(BlockHandle ls, BlockHandle rs)
        {
            return ls.Equals(rs);
        }

        public static bool operator !=(BlockHandle ls, BlockHandle rs)
        {
            return !ls.Equals(rs);
        }

        public override bool Equals(object obj)
        {
            throw new NotSupportedException("Struct boxing not allowed.");
        }
    }

    /// <summary>
    /// Extension methods for BlockHandle BlockAlias conversion
    /// </summary>
    public static class BlockHandleExtensions
    {
        public static BlockAlias ConvertToBlockAlias(this BlockHandle handle, BlockHandleMask mask)
        {
            if(!handle.IsDefined)
                return BlockAlias.Undefined;

            // The handle is XORed with the base mask and a client specific mask
            // to obtain the alias. The '~0x01u' bits ensure that the 'IsDefined'
            // bit is preserved when converting from handle to alias and vice-versa.
            return new BlockAlias(handle.Value ^ (~0x01u & (BlockHandle.DefaultMask ^ mask.Value)));
        }

        public static BlockHandle ConvertToBlockHandle(this BlockAlias alias, BlockHandleMask mask)
        {
            if(!alias.IsDefined)
                return BlockHandle.Undefined;

            // Idem, see above.
            return new BlockHandle(alias.Value ^ (~0x01u & (BlockHandle.DefaultMask ^ mask.Value)));
        }
    }
}