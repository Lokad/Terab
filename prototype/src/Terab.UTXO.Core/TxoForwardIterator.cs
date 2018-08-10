using System;

namespace Terab.UTXO.Core
{
    /// <summary>
    /// Iterator struct to move forward inside a given TxoPack.
    /// </summary>
    public struct TxoForwardIterator
    {
        private int _outpointOffset;

        private int _payloadOffset;

        private readonly TxoPack _packRef;

        public TxoForwardIterator(TxoPack pack)
        {
            _outpointOffset = 0;
            _payloadOffset = 0;
            _packRef = pack;
        }

        /// <summary>
        /// Return current outpoint by reference because Outpoint is a heavy
        /// struct.
        /// </summary>
        public ref Outpoint Current
        {
            get
            {
                var span = _packRef.OutPoints;
                return ref span[_outpointOffset];
            }
        }

        public ReadOnlySpan<byte> CurrentPayload =>
            _packRef.Payloads.Slice(_payloadOffset, Current.PayloadLength);

        public void Reset()
        {
            _outpointOffset = 0;
            _payloadOffset = 0;
        }

        /// <summary>
        /// Indicate that iterator hasn't reached the end.
        /// In other words, we can still get a current element.
        /// </summary>
        public bool EndNotReached => _outpointOffset < _packRef.Count;

        /// <summary>
        /// Shift forward insider anchors.
        /// </summary>
        public void MoveNext()
        {
            if (!EndNotReached)
                throw new IndexOutOfRangeException("TxoForwardIterator index overflows.");

            _payloadOffset += Current.PayloadLength;
            _outpointOffset += 1;
        }
    }
}
