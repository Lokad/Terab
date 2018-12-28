// Copyright Lokad 2018 under MIT BCH.
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace Terab.Lib.Messaging
{
    /// <summary>
    /// Identifier of a temporary (uncommitted) block.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    [DebuggerDisplay("Ucid:{PrettyPrint()}")]
    public unsafe struct UncommittedBlockId : IEquatable<UncommittedBlockId>
    {
        public const int SizeInBytes = 16;

        public fixed byte Data[SizeInBytes];

        public string PrettyPrint()
        {
            var copy = this;
            var hex = new StringBuilder(20);
            for (var i = 0; i < 4; i++)
                hex.AppendFormat("{0:x2}", copy.Data[i]);

            return hex.ToString();
        }

        public static UncommittedBlockId ReadFrom(ReadOnlySpan<byte> span)
        {
            var blockId = new UncommittedBlockId();
            for (var i = 0; i < SizeInBytes; i++)
                blockId.Data[i] = span[i];

            return blockId;
        }

        /// <summary>
        /// Generate an unique <see cref="UncommittedBlockId"/>.
        /// This method is based on
        /// <see cref="System.Security.Cryptography.RNGCryptoServiceProvider"/>.
        /// </summary>
        public static UncommittedBlockId Create()
        {
            Span<byte> buffer = stackalloc byte[SizeInBytes];
            var rng = new RNGCryptoServiceProvider();
            rng.GetBytes(buffer);

            var blockId = new UncommittedBlockId();
            for (var i = 0; i < SizeInBytes; i++)
                blockId.Data[i] = buffer[i];

            return blockId;
        }

        public bool Equals(UncommittedBlockId other)
        {
            for (var i = 0; i < SizeInBytes; i++)
                if (Data[i] != other.Data[i]) return false;

            return true;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            return obj is UncommittedBlockId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Data[0] + Data[1] >> 8 + Data[2] >> 16 + Data[3] >> 24;
        }
    }
}