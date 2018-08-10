using System;

namespace Terab.UTXO.Core
{

    /// <summary>
    /// Struct to memorize anchors of last writing operation into TxoPack.
    /// Provides a method to write outpoint and payload in the respectively
    /// appropriate places in memory. 
    /// </summary>
    public struct TxoPackWriter
    {
        public bool IsEmpty => _outpointOffset == 0;

        /// <summary>
        /// This offset keeps track of the amount of outpoint already written.
        /// </summary>
        private int _outpointOffset;

        /// <summary>
        /// This offset keeps track of the position in the Payload span
        /// for the next payload to be written.
        /// </summary>
        private int _payloadOffsetInBytes;

        private readonly TxoPack _pack;

        internal TxoPackWriter(TxoPack pack)
        {
            _pack = pack ?? throw new ArgumentNullException(
                        nameof(pack), "Cannot create TxoPackWriter with null TxoPack.");

            _outpointOffset = 0;
            _payloadOffsetInBytes = 0;
        }

        /// <summary>
        /// Provide full access to sub payload writer <see cref="PayloadWriter"/>
        /// to operate complex payload writing.
        /// </summary>
        public Span<byte> RemainingPayload => _pack.Payloads.Slice(_payloadOffsetInBytes);

        /// <summary>
        /// Write an outpoint and a payload into member TxoPack.
        /// </summary>
        public void Write(in Outpoint outpoint, ReadOnlySpan<byte> payload)
        {
            Write(payload);
            Write(in outpoint);
        }

        /// <summary>
        /// Write a payload or part of a payload into member TxoPack.
        /// </summary>
        /// <remarks>
        /// This method is useful for the payload merge when parts of the
        /// payload are added little by little. Like this, less additional
        /// temporary buffer is necessary.
        /// </remarks>
        private void Write(ReadOnlySpan<byte> payload)
        {
            if (_payloadOffsetInBytes + payload.Length > _pack.Payloads.Length)
                throw new ArgumentOutOfRangeException(nameof(payload),
                    "Trying to write over TxoPack max payloads capacity.");

            var destPayload = _pack.Payloads.Slice(_payloadOffsetInBytes, payload.Length);
            payload.CopyTo(destPayload);
            _payloadOffsetInBytes += payload.Length;
            _pack.PayloadsLength = _payloadOffsetInBytes;
        }

        /// <summary>
        /// Write an outpoint into member TxoPack.
        /// </summary>
        private void Write(in Outpoint outpoint)
        {
            if (_outpointOffset >= _pack.OutPoints.Length)
                throw new ArgumentOutOfRangeException(nameof(outpoint),
                    "Trying to write over TxoPack max outpoints capacity.");

            var span = _pack.OutPoints;
            span[_outpointOffset] = outpoint;
            _pack.Count = ++_outpointOffset;
        }

        /// <summary>
        /// Reset state of writer as it was freshly created
        /// </summary>
        public void Reset()
        {
            _outpointOffset = 0;
            _payloadOffsetInBytes = 0;
        }

        /// <summary>
        /// Whenever two txo sets are being merged, it will probably happen
        /// that the end of the one is reached before the end of the other.
        /// Therefore, the merging process is finished, and the remaining 
        /// Iterator that goes through the rest of the txo set to copy, from the
        /// point on where the comparison ended.
        /// </summary>
        /// <param name="iter">
        /// </param>
        public void WriteMany(ref TxoForwardIterator iter)
        {
            for (; iter.EndNotReached; iter.MoveNext())
            {
                // outpoint is written, but no payload
                Write(in iter.Current, iter.CurrentPayload);
            }
        }
    }
}