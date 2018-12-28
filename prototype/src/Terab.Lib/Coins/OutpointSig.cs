// Copyright Lokad 2018 under MIT BCH.
using System;

namespace Terab.Lib.Coins
{
    /// <summary>
    /// Compact signature intended for a naive probabilistic filter
    /// within the first layer of the Sozu table.
    /// </summary>
    public struct OutpointSig : IEquatable<OutpointSig>
    {
        public static readonly int SizeInBytes = sizeof(ushort);

        public ushort Value { get; private set; }

        public OutpointSig(ushort value)
        {
            Value = value;
        }

        /// <summary>
        /// Pseudo-hash of the outpoint hash, keeping only the 16 lower bits.
        /// </summary>
        public static OutpointSig From(ulong outpointHash)
        {
            const ulong Mask = 0xFFFF;
            return new OutpointSig((ushort)(outpointHash & Mask));
        }

        public bool Equals(OutpointSig other)
        {
            return Value == other.Value;
        }
    }
}