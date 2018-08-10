using System;

namespace Terab.UTXO.Core
{
    /// <summary>
    /// Permits deserialization of a memory section containing a
    /// transaction payload associated with an outpoint.
    /// A correctly encoded such memory section contains the monetary
    /// amount in the transaction as well as information
    /// on the blocks when that amount has been received or spent.
    ///
    /// The encoding of the payload is as follows:
    ///  - first a ulong representing the satoshis
    ///  - then the data length followed by the data. The space used for writing
    ///      the data length is not included in the length indication
    ///  - lastly, array of BlockEvents . Their number is not encoded, the
    /// length of the span indicates when all events have been read.
    /// </summary>
    /// <remarks>
    /// From a pure-Bitcoin perspective, there is no such thing as
    /// "multi-lineage" as only the longest block chain matter. However, as
    /// blocks may be orphaned there is a need to be capable of handling
    /// multiple lineages within the TXO table.
    /// </remarks>
    public static class PayloadReader
    {
        /// <summary>
        /// Monetary amount associated with the transaction output.
        /// </summary>
        public static ulong GetSatoshis(ReadOnlySpan<byte> payload)
        {
            if (payload.Length < sizeof(ulong))
                throw new ArgumentException($"Span of size {payload.Length} cannot be parsed as payload.");

            return BitConverter.ToUInt64(payload);
        }

        /// <summary>
        /// Returns the data section of the payload.
        /// </summary>
        public static ReadOnlySpan<byte> GetData(ReadOnlySpan<byte> payload)
        {
            var length = GetDataLength(payload);
            if(length > payload.Length - sizeof(ulong) - sizeof(int))
                throw new ArgumentException($"Span of size {payload.Length} cannot contain data of length {length}.");

            return payload.Slice(sizeof(ulong) + sizeof(int), length);
        }

        /// <summary>
        /// Returns the length of the data in the payload. As only this length
        /// and the total length of the payload are known, this information is
        /// necessary to know where to start decoding the events which follow
        /// the data section.
        /// </summary>
        public static int GetDataLength(ReadOnlySpan<byte> rawPayload)
        {
            if (rawPayload.Length < sizeof(ulong) + sizeof(int))
                throw new ArgumentException($"Span of size {rawPayload.Length} cannot be parsed as payload.");

            return BitConverter.ToInt32(rawPayload.Slice(sizeof(ulong)));
        }

        private static ReadOnlySpan<byte> GetSatoshisAndData(ReadOnlySpan<byte> payload)
        {
            if (payload.Length < sizeof(ulong) + sizeof(int))
                throw new ArgumentException($"Span of size {payload.Length} cannot be parsed as payload.");

            return payload.Slice(0, GetDataLength(payload) + sizeof(ulong) + sizeof(int));
        }

        /// <summary>
        /// Get a struct iterator on payload events.
        /// </summary>
        public static PayloadEventIterator GetEvents(ReadOnlySpan<byte> payload)
        {
            var offset = GetEventsOffset(payload);
            
            return new PayloadEventIterator(payload.Slice(offset));
        }

        /// <summary>
        /// The age of a payload is considered to be its oldest production
        /// event.
        /// </summary>
        /// <remarks>
        /// As events are ordered in chronological order, we just need to
        /// retrive the first event.
        /// </remarks>
        public static uint GetAge(ReadOnlySpan<byte> payload)
        {
            var iter = GetEvents(payload);
            return iter.Current.BlockAlias.Value;
        }

        private static int GetEventsOffset(ReadOnlySpan<byte> payload)
        {
            // start of the events part of the payload
            var offset = sizeof(ulong) + sizeof(int) + GetDataLength(payload); 
            if (offset > payload.Length)
                throw new ArgumentException($"Array of size {payload.Length} cannot contain events.");

            return offset;
        }

        /// <summary>
        /// Merge two payloads into one
        /// </summary>
        /// <param name="p1"> a payload </param>
        /// <param name="p2"> another payload </param>
        /// <param name="writer">
        /// payload writer to write the merged payload
        /// </param>
        public static void MergePayloads(ReadOnlySpan<byte> p1, ReadOnlySpan<byte> p2, ref PayloadWriter writer)
        {
            if (GetSatoshis(p1) != GetSatoshis(p2))
                throw new ArgumentException(
                    $"Satoshi values not aligned: ${GetSatoshis(p1)} vs ${GetSatoshis(p2)}. No merge possible.");

            if (!GetData(p1).SequenceEqual(GetData(p2)))
                throw new ArgumentException($"Data not aligned. No merge possible.");

            writer.Write(GetSatoshisAndData(p1));

            var events1 = GetEvents(p1);
            var events2 = GetEvents(p2);

            while (events1.EndNotReached && events2.EndNotReached)
            {
                var current1 = events1.Current;
                var current2 = events2.Current;
                var compareRes = current1.CompareTo(current2);

                if (compareRes < 0)
                {
                    writer.Write(current1);
                    events1.MoveNext();
                }
                else if (compareRes > 0)
                {
                    writer.Write(current2);
                    events2.MoveNext();
                }
                else if (compareRes == 0)
                {
                    writer.Write(current1);
                    events1.MoveNext();
                    events2.MoveNext();
                }
            }

            writer.WriteRemainingEvents(ref events1);
            writer.WriteRemainingEvents(ref events2);
        }
    }
}