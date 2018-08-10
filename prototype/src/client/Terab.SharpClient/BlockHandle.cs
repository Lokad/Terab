using System;

namespace Terab.Client
{
    [System.Diagnostics.DebuggerDisplay("[Block #{Value}]")]
    public struct BlockHandle : IEquatable<BlockHandle>
    {
        public readonly Int32 Value;

        internal BlockHandle(Int32 handleValue)
        {
            Value = handleValue;
        }

        public static readonly BlockHandle Zero = default(BlockHandle);

        public bool Equals(BlockHandle other)
        {
            return Value == other.Value;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            return obj is BlockHandle && Equals((BlockHandle) obj);
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }

        public static bool operator ==(BlockHandle left, BlockHandle right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(BlockHandle left, BlockHandle right)
        {
            return !left.Equals(right);
        }
    }
}