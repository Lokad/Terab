using System;
using System.Diagnostics.Contracts;
using Terab.UTXO.Core.Hash;

namespace Terab.UTXO.Core
{
    /// <summary>
    /// Flags for Terab extended properties of Outpoint.
    /// </summary>
    /// <remarks>
    /// LargeScript : <see cref="Constants.LargeScriptLength"/>
    /// </remarks>
    [Flags]
    public enum OutpointFlags : ushort
    {
        None = 0,
        IsLargeScript = 1 << 0,
        IsCoinBase = 1 << 1
    }

    /// <summary>
    /// Extension method for enum OutpointFlags.
    /// </summary>
    internal static class OutpointFlagsExtensions
    {
        public static OutpointFlags ToOutpointFlags(this ushort value)
        {
            return (OutpointFlags) value;
        }
    }


    /// <summary>
    /// An extended representation the Bitcoin outpoint which uniquely
    /// identify a transaction output.
    /// An outpoint is organized as follow:
    ///  -- 32 bytes of transaction_id, which is a hash
    ///  --  4 bytes for index
    ///   That's the key part of outpoint, aligned with Bitcoin definition.
    /// Plus
    ///  -- 2 bytes for corresponding payload length
    ///  -- 2 bytes for flags
    ///   That's Terab specific.
    /// </summary>
    /// <remarks>
    /// This struct extends the definition of an outpoint of Bitcoin,
    /// as we added extra properties like OutpointFlags and PayloadLength.
    /// Although we still use transaction_id plus index as key for
    /// equality check and ordering, some extra properties are persisted for
    /// the sake of Sozu table querying.
    /// </remarks>
    public struct Outpoint : IComparable<Outpoint>, IEquatable<Outpoint>
    {
        public const int KeySizeInBytes = 36;

        public const int TotalSizeInBytes = 40;

        public TxId Txid { get; }

        public int Index { get; }

        public ushort PayloadLength { get; }

        public OutpointFlags Flags { get; }


        public static Outpoint Create(ReadOnlySpan<byte> span)
        {
            if (span.Length != TotalSizeInBytes)
                throw new ArgumentException(nameof(span));

            var txid = new TxId(span.Slice(0, TxId.SizeInBytes));
            var index = BitConverter.ToInt32(span.Slice(TxId.SizeInBytes, sizeof(int)));
            var payloadLength = BitConverter.ToUInt16(span.Slice(KeySizeInBytes, sizeof(ushort)));
            var flags = BitConverter.ToUInt16(span.Slice(KeySizeInBytes, sizeof(ushort))).ToOutpointFlags();

            return new Outpoint(txid, index, payloadLength, flags);
        }

        /// <summary>
        /// Factory to create outpoint with only Txid + Index,
        /// outpoint in the canonical Bitcoin perspective.
        /// </summary>
        public static Outpoint CreateNaked(ReadOnlySpan<byte> span)
        {
            if (span.Length != KeySizeInBytes)
                throw new ArgumentException(nameof(span));

            var txid = new TxId(span.Slice(0, TxId.SizeInBytes));
            var index = BitConverter.ToInt32(span.Slice(TxId.SizeInBytes, sizeof(int)));

            return new Outpoint(txid, index, 0, OutpointFlags.None);
        }

        public Outpoint(TxId txid, int index, int payloadLength, OutpointFlags flags)
        {
            if (payloadLength > ushort.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(payloadLength),
                    "Cannot exceeds 65556 bytes.");


            Txid = txid;
            Index = index;
            PayloadLength = (ushort) payloadLength;
            Flags = flags;
        }

        public Outpoint(TxId txid, int index, int payloadLength)
            : this(txid, index, payloadLength, OutpointFlags.None)
        {
        }

        /// <summary>
        /// Interprets the outpoint as a large unsigned integer, and
        /// returns the largest 'n' bits as an integer.
        /// </summary>
        [Pure]
        public int Top(int bits)
        {
            return Txid.Top(bits);
        }

        public int CompareTo(Outpoint other)
        {
            var txidComparison = Txid.CompareTo(other.Txid);
            if (txidComparison != 0) return txidComparison;
            return Index.CompareTo(other.Index);
        }

        public bool Equals(Outpoint other)
        {
            return Txid.Equals(other.Txid) && Index == other.Index;
        }

        public override bool Equals(object obj)
        {
            return obj is Outpoint outpoint && Equals(outpoint);
        }

        public static bool operator ==(Outpoint lp, Outpoint rp)
        {
            return lp.Equals(rp);
        }

        public static bool operator !=(Outpoint lp, Outpoint rp)
        {
            return !lp.Equals(rp);
        }

        public static bool operator <(Outpoint lp, Outpoint rp)
        {
            return lp.CompareTo(rp) == -1;
        }

        public static bool operator >(Outpoint lp, Outpoint rp)
        {
            return lp.CompareTo(rp) == 1;
        }

        public static bool operator >=(Outpoint lp, Outpoint rp)
        {
            return lp.CompareTo(rp) >= 0;
        }

        public static bool operator <=(Outpoint lp, Outpoint rp)
        {
            return lp.CompareTo(rp) <= 0;
        }

        public override int GetHashCode()
        {
            // Outpoints are not intended to be operated through dictionaries or collections
            throw new NotSupportedException();
        }

        public void WriteTo(Span<byte> buffer)
        {
            Txid.WriteTo(buffer.Slice(0, KeySizeInBytes - sizeof(int)));
            BitConverter.TryWriteBytes(buffer.Slice(KeySizeInBytes - sizeof(int), sizeof(int)), Index);
            BitConverter.TryWriteBytes(buffer.Slice(KeySizeInBytes, sizeof(ushort)), PayloadLength);
            BitConverter.TryWriteBytes(buffer.Slice(KeySizeInBytes + sizeof(ushort), sizeof(ushort)), (ushort) Flags);
        }
    }
}