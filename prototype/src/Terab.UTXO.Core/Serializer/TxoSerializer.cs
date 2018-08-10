using System;
using Terab.UTXO.Core.Sozu;

namespace Terab.UTXO.Core.Serializer
{
    /// <summary>
    /// Sector extension method for serialize a TxoPack to Sector.
    /// and deserialize a TxoPack from a Sector.
    /// </summary>
    /// <remarks>
    /// Sector <see cref="Sector"/> has its own structure, this
    /// class uses only sector's content.
    /// Serialization format is organized as follow:
    /// -- child sector index
    /// -- number of txos in TxoPack
    /// -- array of extended Outpoints
    /// -- array of Payloads
    /// This format is tightly coupled with TxoPack <see cref="TxoPack"/>
    /// inside representation, array of payloads is manipulated as a whole
    /// without any slicing during serialization.
    /// </remarks>
    public static class TxoSerializer
    {
        /// <summary>
        /// Serie of start position for serialization format.
        /// </summary>
        public const int ChildSectorIndexStart = 0;

        public const int ChildSectorIndexSizeInBytes = sizeof(long);
        public const int TxoCountStart = ChildSectorIndexStart + ChildSectorIndexSizeInBytes;
        public const int TxoCountSizeInBytes = sizeof(int);
        public const int TxoStart = TxoCountStart + TxoCountSizeInBytes;

        /// <summary>
        /// Deserialize a TxoPack from Sector.
        /// </summary>
        /// <param name="sector">Sector to deserialize</param>
        /// <param name="pack">TxoPack to fill</param>
        /// <param name="childSectorIndex">sectorIndex of child</param>
        /// <remarks>
        /// Life-time of the response lasts until the next call to 'Read'.
        /// Size of TxoPack should be large enough to not overflow.
        /// </remarks>
        public static void Deserialize(this Sector sector, TxoPack pack, out SectorIndex childSectorIndex)
        {
            var sectorContent = sector.Contents;

            childSectorIndex =
                new SectorIndex(BitConverter.ToInt32(sectorContent.Slice(ChildSectorIndexStart, ChildSectorIndexSizeInBytes)));
            var numberOfTxo = BitConverter.ToInt32(sectorContent.Slice(TxoCountStart, TxoCountSizeInBytes));
            var payloadLengthAccumulator = 0;

            ref var writer = ref pack.GetResetWriter();

            var offsetLimit = TxoStart + numberOfTxo * Outpoint.TotalSizeInBytes;
            for (var offset = TxoStart;
                offset < offsetLimit;
                offset += Outpoint.TotalSizeInBytes)
            {
                var p = Outpoint.Create(sectorContent.Slice(offset, Outpoint.TotalSizeInBytes));
                writer.Write(in p, sectorContent.Slice(offsetLimit + payloadLengthAccumulator, p.PayloadLength));
                payloadLengthAccumulator += p.PayloadLength;
            }
        }

        /// <summary>
        /// Serialize a TxoPack to Sector.
        /// </summary>
        /// <param name="sector">
        /// Sector where serialized TxoPack would be saved
        /// </param>
        /// <param name="pack">TxoPack to serialize</param>
        /// <param name="childSectorIndex">sectorIndex of child</param>
        /// <remarks>we don't check sector size overflow at this level</remarks>
        public static void Serialize(this Sector sector, TxoPack pack, SectorIndex childSectorIndex)
        {
            var sectorContent = sector.Contents;

            BitConverter.TryWriteBytes(sectorContent.Slice(ChildSectorIndexStart, ChildSectorIndexSizeInBytes), childSectorIndex.Value);
            BitConverter.TryWriteBytes(sectorContent.Slice(TxoCountStart, TxoCountSizeInBytes), pack.Count);

            var offset = TxoStart;
            for (var iter = pack.GetForwardIterator(); iter.EndNotReached; iter.MoveNext())
            {
                iter.Current.WriteTo(sectorContent.Slice(offset, Outpoint.TotalSizeInBytes));
                offset += Outpoint.TotalSizeInBytes;
            }

            pack.Payloads.Slice(0, pack.PayloadsLength).CopyTo(sectorContent.Slice(offset, pack.PayloadsLength));
        }
    }
}