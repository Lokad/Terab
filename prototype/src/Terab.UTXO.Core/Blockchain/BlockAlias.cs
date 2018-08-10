using System;

namespace Terab.UTXO.Core.Blockchain
{
    /// <summary>
    /// Dedicated type to identify a block inide Terab
    /// Wrap an underlying UInt32 with own limit.
    /// </summary>
    public struct BlockAlias : IEquatable<BlockAlias>
    {
        public static BlockAlias Undefined = new BlockAlias(Constants.MaxBlockAliasValue);

        public static BlockAlias GenesisParent = new BlockAlias(0);

        public static BlockAlias Genesis = new BlockAlias(1);

        private readonly uint _value;

        public BlockAlias(uint value)
        {
            if (value > Constants.MaxBlockAliasValue)
                throw new ArgumentOutOfRangeException(nameof(value));

            _value = value;
        }

        public uint Value => _value;


        public bool Equals(BlockAlias other)
        {
            return this._value == other._value;
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

        /// <summary>
        /// Return true if and only if this BlockAlias is prior to argument
        /// BlockAlias.
        /// </summary>
        public bool IsPrior(BlockAlias other)
        {
            if (this == BlockAlias.Undefined || other == BlockAlias.Undefined)
                throw new ArgumentException("Trying IsPrior on undefined BlockAlias");

            return this._value.CompareTo(other._value) < 0;
        }

        public BlockAlias GetNext() => new BlockAlias(_value + 1);

        /// <summary>
        /// Get a new BlockAlias with underlying value is superior
        /// to both input alias.
        /// </summary>
        public static BlockAlias GetJoinNext(BlockAlias b1, BlockAlias b2)
        {
            return new BlockAlias(Math.Max(b1._value, b2._value) + 1);
        }
    }
}