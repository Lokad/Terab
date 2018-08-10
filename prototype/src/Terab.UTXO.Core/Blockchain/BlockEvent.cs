using System;

namespace Terab.UTXO.Core.Blockchain
{
    /// <summary>
    /// Inside a block, when an outpoint is produced or consumed,
    /// we call it an event. The enum materialzes different types
    /// of events.
    /// </summary>
    public enum BlockEventType : uint
    {
        Production = 0,
        Consumption = 1,
    }

    /// <summary>
    /// Extension method for enum BlockEventType.
    /// </summary>
    internal static class BlockEventTypeExtensions
    {
        /// <summary>
        /// Use this method to create BlockEventType instead of direct
        /// cast.
        /// </summary>
        public static BlockEventType ToBlockEventType(this uint type)
        {
            return (BlockEventType) type;
        }
    }


    /// <summary>
    /// Inside a block, when an outpoint is produced or consumed,
    /// we call it an event. Event type plus its blockalias are
    /// encapsulated in BlockEvent struct.
    /// </summary>
    /// <remarks>
    /// As BlockAlias uses only lower 3 bytes of uint, BlockEvent
    /// uses higher bits to encode BlockEventType.
    /// <see cref="BlockAlias"/>
    /// </remarks>
    public struct BlockEvent : IEquatable<BlockEvent>, IComparable<BlockEvent>
    {
        public const int SizeInBytes = 4;

        /// <summary>
        /// Mask for get the BlockAlias value
        /// 24 because blockalias uses only
        /// lowest 3 bytes of BlockEvent backing uint.
        /// </summary>
        private const int Mask = (1 << 24) - 1;

        /// <summary>
        /// We use the highest bit of unit to flag the BlockEventType.
        /// </summary>
        private const int Shift = 31;

        private readonly uint _value;

        public BlockAlias BlockAlias => new BlockAlias(_value & Mask);

        public BlockEventType Type => (_value >> Shift).ToBlockEventType();

        /// <summary>
        /// Intended for serialization.
        /// </summary>
        public uint Value => _value;

        public BlockEvent(BlockAlias alias, BlockEventType type)
        {
            _value = alias.Value | (((uint) type) << Shift);
        }

        /// <summary>
        /// Intended for deserialization.
        /// </summary>
        /// <param name="raw">raw value of a BlockEvent</param>
        public BlockEvent(uint raw)
        {
            _value = raw;
        }

        public bool Equals(BlockEvent other)
        {
            return _value.Equals(other._value);
        }

        /// <summary>
        /// The comparer logic is based on chronological order.
        /// We can directly compare backing raw unit, as consumption
        /// event is always greater (happen first) than production event.
        /// </summary>
        /// <remarks>
        /// We need to review the ordering if we use other higher bits of
        /// uint for other flags.
        /// </remarks>
        public int CompareTo(BlockEvent x)
        {
            return _value.CompareTo(x.Value);
        }
    }
}