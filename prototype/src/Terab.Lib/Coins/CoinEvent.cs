// Copyright Lokad 2018 under MIT BCH.
using System;
using Terab.Lib.Chains;

namespace Terab.Lib.Coins
{
    /// <summary>
    /// Represents different types of events.
    /// </summary>
    /// <remarks>
    /// Inside a block, a coin event is production or consumption of an outpoint.
    /// </remarks>
    public enum CoinEventKind : uint
    {
        Production = 0,
        Consumption = 1
    }

    internal static class CoinEventTypeExtensions
    {
        /// <summary>
        /// Explicit conversion of the type.
        /// </summary>
        public static CoinEventKind ToCoinEventKind(this uint kind)
        {
            return (CoinEventKind) kind;
        }
    }


    /// <summary>
    /// Encapsulates a <see cref="CoinEventKind"/> and the
    /// corresponding <see cref="BlockAlias"/>.
    /// </summary>
    /// <remarks>
    /// As <see cref="BlockAlias"/> uses only the lower 3 bytes of uint,
    /// <see cref="CoinEvent"/> uses higher bits to encode the
    /// <see cref="CoinEventKind"/>.
    /// </remarks>
    public struct CoinEvent : IEquatable<CoinEvent>
    {
        public const int SizeInBytes = 4;

        /// <summary>
        /// Bitmask for extracting the <see cref="BlockAlias"/> value.
        /// 24 because <see cref="BlockAlias"/> uses only the
        /// lowest 3 bytes of the <see cref="CoinEvent"/> backing uint.
        /// </summary>
        private const int BlockAliasMask = 0x7FFFFFFF;

        /// <summary>
        /// Highest bit of unit is used to flag the <see cref="CoinEventKind"/>.
        /// </summary>
        private const int Shift = 31;

        private readonly uint _value;

        public BlockAlias BlockAlias => new BlockAlias(_value & BlockAliasMask);

        public CoinEventKind Kind => (_value >> Shift).ToCoinEventKind();

        /// <summary>
        /// Intended for serialization.
        /// </summary>
        public uint Value => _value;

        public CoinEvent(BlockAlias alias, CoinEventKind kind)
        {
            var eventMask = (uint) kind << Shift;
            _value = alias.Value | eventMask;
        }

        public bool Equals(CoinEvent other)
        {
            return _value == other._value;
        }

        public override int GetHashCode()
        {
            return (int) _value;
        }
    }
}