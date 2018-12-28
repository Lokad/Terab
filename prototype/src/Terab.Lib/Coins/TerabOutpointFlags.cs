// Copyright Lokad 2018 under MIT BCH.
using System;
using Terab.Lib.Messaging;

namespace Terab.Lib.Coins
{
    /// <summary>
    /// Flags for extended properties of an outpoint from the Terab perspective.
    /// </summary>
    [Flags]
    public enum TerabOutpointFlags : ushort
    {
        None,
        PersistentIsCoinbase,
    }

    public static class TerabOutpointFlagsExtensions
    {
        public static OutpointFlags ToClientFlags(this TerabOutpointFlags _this)
        {
            // Not using 'Enum.HasFlags' to avoid an accidental object allocation.
            if (_this == TerabOutpointFlags.PersistentIsCoinbase)
                return OutpointFlags.IsCoinbase;

            return OutpointFlags.None;
        }
    }
}