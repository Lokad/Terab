// Copyright Lokad 2018 under MIT BCH.
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace Terab.Lib.Messaging
{
    /// <summary>
    /// Canonical outpoint in Bitcoin. Uniquely identify each coin.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    [DebuggerDisplay("{PrettyPrint()}")]
    public unsafe struct Outpoint : IEquatable<Outpoint>
    {
        public const int SizeInBytes = 36;

        public fixed byte TxId[32];
        public int TxIndex;

        private string PrettyPrint()
        {
            {
                var copy = this;
                var hex = new StringBuilder(30);
                for (var i = 0; i < 4; i++)
                    hex.AppendFormat("{0:x2}", copy.TxId[i]);

                hex.Append("-");
                hex.Append(TxIndex);

                return hex.ToString();
            }
        }

        public bool Equals(Outpoint other)
        {
            for (var i = 0; i < 32; i++)
                if (TxId[i] != other.TxId[i]) return false;

            return TxIndex == other.TxIndex;
        }

        public override int GetHashCode()
        {
            return TxId[0] + TxId[1] >> 8 + TxId[2] >> 16 + TxId[3] >> 24;
        }
    }
}