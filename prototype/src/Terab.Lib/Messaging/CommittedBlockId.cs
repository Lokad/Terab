// Copyright Lokad 2018 under MIT BCH.
using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace Terab.Lib.Messaging
{
    /// <summary>
    /// Canonical unique identifier of a Bitcoin block.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    [DebuggerDisplay("Bid:{PrettyPrint()}")]
    public unsafe struct CommittedBlockId : IEquatable<CommittedBlockId>
    {
        public const int SizeInBytes = 32;

        public static readonly CommittedBlockId GenesisParent  = default;

        /// <summary> Only intended for testing purposes. </summary>
        internal static readonly CommittedBlockId Genesis = ReadFromHex(Consensus.GenesisBlockIdHex);

        public fixed byte Data[SizeInBytes];

        private string PrettyPrint()
        {
            var copy = this;
            var hex = new StringBuilder(20);
            for (var i = 0; i < 4; i++)
                hex.AppendFormat("{0:x2}", copy.Data[i]);

            return hex.ToString();
        }

        /// <summary> Low performance implementation. </summary>
        public static CommittedBlockId ReadFrom(ReadOnlySpan<byte> span)
        {
            var blockId = new CommittedBlockId();
            for (var i = 0; i < SizeInBytes; i++)
                blockId.Data[i] = span[i];

            return blockId;
        }

        /// <summary> Low performance implementation. </summary>
        public static CommittedBlockId ReadFromHex(string hex)
        {
            var data = Enumerable.Range(0, hex.Length)
                .Where(x => x % 2 == 0)
                .Select(x => Convert.ToByte(hex.Substring(x, 2), 16))
                .ToArray();

            return ReadFrom(data);
        }

        public bool Equals(CommittedBlockId other)
        {
            for (var i = 0; i < SizeInBytes; i++)
                if(Data[i] != other.Data[i]) return false;

            return true;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            return obj is CommittedBlockId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Data[0] + Data[1] >> 8 + Data[2] >> 16 + Data[3] >> 24;
        }
    }
}