using System;

namespace Terab.Client
{
    public struct BlockUcid : IEquatable<BlockUcid>
    {
        public readonly UInt64 Left, Right;

        internal BlockUcid(UInt64 left, UInt64 right)
        {
            Left = left; Right = right;
        }

        public bool Equals(BlockUcid other)
        {
            return Left== other.Left && Right == other.Right;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            return obj is BlockUcid && Equals((BlockUcid) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (Left.GetHashCode() * 397) ^ Right.GetHashCode();
            }
        }

        public static bool operator ==(BlockUcid left, BlockUcid right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(BlockUcid left, BlockUcid right)
        {
            return !left.Equals(right);
        }
    }
}