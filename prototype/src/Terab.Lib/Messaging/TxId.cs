// Copyright Lokad 2018 under MIT BCH.
using System;
using System.Runtime.InteropServices;

namespace Terab.Lib.Messaging
{
    /// <summary>
    /// Canonical unique identifier of Bitcoin transaction.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public unsafe struct TxId : IEquatable<TxId>
    {
        public const int SizeInBytes = 32;

        public fixed byte Data[SizeInBytes];

        public bool Equals(TxId other)
        {
            for (var i = 0; i < SizeInBytes; i++)
                if (Data[i] != other.Data[i]) return false;

            return true;
        }
        public override int GetHashCode()
        {
            return Data[0] + Data[1] >> 8 + Data[2] >> 16 + Data[3] >> 24;
        }
    }
}