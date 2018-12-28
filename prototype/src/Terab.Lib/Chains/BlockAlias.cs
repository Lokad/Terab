// Copyright Lokad 2018 under MIT BCH.
using System;
using System.Diagnostics;
using Terab.Lib.Coins;

namespace Terab.Lib.Chains
{
    /// <summary>
    /// Dedicated type to identify a block inside Terab.
    /// It wraps an underlying UInt32 with a given limit.
    /// </summary>
    /// <remarks>
    /// The BlockAlias is made of - starting from the least significant bits:
    /// - 1 bit, isDefined
    /// - 6 bits, subIndex 
    /// - 24 bits, blockheight
    /// - 1 bit, reserved for <see cref="CoinEvent"/>
    ///   more than one block at the same height.
    /// </remarks>
    [DebuggerDisplay("{PrettyPrint}")]
    public struct BlockAlias : IEquatable<BlockAlias>
    {
        /// <summary>
        /// Intended to support in-place a block alias conversion as a coin event,
        /// by leaving the 8 higher bits unused.
        /// </summary>
        public const int MaxBlockHeight = (1 << 24) - 1;

        public static BlockAlias Undefined = new BlockAlias(0x00);

        public static BlockAlias GenesisParent = new BlockAlias(0, SubIndexMaxValue);

        public static BlockAlias Genesis = new BlockAlias(0, 0);

        public const int SizeInBytes = 4;

        private const int SubIndexMaxValue = 63;
        private const uint BlockHeightMask = 0x7F_FF_FF_80;
        private const uint SubIndexMask = 0x00_00_00_7F;

        private readonly uint _value;

        public string PrettyPrint
        {
            get
            {
                if (!IsDefined) return "Undefined";
                if (this == GenesisParent) return "GenesisParent";
                if (this == Genesis) return "Genesis";
                return $"{GetBlockHeight(_value)}:{GetSubIndex(_value)}";
            }
        }

        /// <summary>
        /// Verify that the given values will make for a valid
        /// <see cref="BlockAlias"/>:
        /// - the blockHeight must be less than 24 bits large
        /// - the subIndex must be less than 6 bits large
        /// </summary>
        /// <returns>Once checked, return the <see cref="BlockAlias"/>
        /// raw value.</returns>
        private static uint CheckValue(int blockHeight, int subIndex)
        {
            if (blockHeight > MaxBlockHeight)
                throw new ArgumentOutOfRangeException(nameof(blockHeight));
            if (subIndex > SubIndexMaxValue)
                throw new ArgumentOutOfRangeException(nameof(subIndex));

            return (uint) ((blockHeight << 7) | (subIndex << 1) | 0x01);
        }

        private static int GetBlockHeight(uint value)
        {
            return (int) ((value & BlockHeightMask) >> 7);
        }

        private static int GetSubIndex(uint value)
        {
            return (int) ((value & SubIndexMask) >> 1);
        }

        public BlockAlias(uint raw)
        {
            _value = raw;
        }

        public BlockAlias(int blockHeight, int subIndex)
        {
            _value = CheckValue(blockHeight, subIndex);
        }

        public uint Value => _value;

        public bool IsDefined => (_value & 0x01) != 0;

        public int BlockHeight => GetBlockHeight(_value);

        public int SubIndex => GetSubIndex(_value);

        public bool Equals(BlockAlias other)
        {
            return _value == other._value;
        }

        public override bool Equals(object obj)
        {
            throw new NotSupportedException();
        }

        public override int GetHashCode()
        {
            return _value.GetHashCode();
        }

        public static bool operator ==(BlockAlias ls, BlockAlias rs)
        {
            return ls.Equals(rs);
        }

        public static bool operator !=(BlockAlias ls, BlockAlias rs)
        {
            return !ls.Equals(rs);
        }

        public override string ToString()
        {
            return $"BlockAlias {_value}";
        }

        public bool IsUndefined => this == Undefined;

        /// <summary>
        /// Return true if and only if this <see cref="BlockAlias"/> is prior to
        /// argument <see cref="BlockAlias"/>.
        /// </summary>
        public bool IsPrior(BlockAlias other)
        {
            if (IsUndefined || other.IsUndefined)
                throw new ArgumentException("Trying IsPrior on undefined BlockAlias");

            return _value < other._value;
        }

        /// <summary>
        /// Return true if and only if this <see cref="BlockAlias"/> is prior or
        /// equal to argument <see cref="BlockAlias"/>.
        /// </summary>
        public bool IsPriorOrEqual(BlockAlias other)
        {
            if (IsUndefined || other.IsUndefined)
                throw new ArgumentException("Trying IsPriorOrEqual on undefined BlockAlias");

            return _value <= other._value;
        }

        /// <summary>
        /// Returns a block alias that is <see cref="distance"/> before
        /// the current alias, in terms of blockchain height. The subindex of
        /// the returned alias is always set to 0.
        /// If 'distance' is bigger than the blockchain height of
        /// the current alias, the Genesis parent alias is returned.
        /// </summary>
        public BlockAlias GetPriorAlias(int distance)
        {
            var futureBlockHeight = BlockHeight - distance;
            if (futureBlockHeight > 0)
                return new BlockAlias(futureBlockHeight, 0);

            return futureBlockHeight == 0 ? Genesis : GenesisParent;
        }
    }
}