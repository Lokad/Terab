using System;
using Terab.UTXO.Core.Blockchain;

namespace Terab.UTXO.Core
{
    /// <summary>
    /// Intends to help complex payload writing (ie: merging payloads of equal
    /// outpoint).
    /// </summary>
    /// <remarks>Stack only</remarks>
    public ref struct PayloadWriter
    {
        private Span<byte> _buffer;

        /// <summary>
        /// recording writing position
        /// </summary>
        private int _offset;

        /// <summary>
        /// recording number of written events
        /// </summary>
        private int _eventCount;

        public PayloadWriter(Span<byte> buffer)
        {
            _buffer = buffer;
            _offset = 0;
            _eventCount = 0;
        }

        /// <summary>
        /// Return number of written events.
        /// </summary>
        public int EventCount => _eventCount;

        /// <summary>
        /// Current writing offset from the start, also accumulate length of what was
        /// written so far.
        /// </summary>
        public int Offset => _offset;

        /// <summary>
        /// Write ONLY no events part of a payload into buffer span.
        /// </summary>
        /// <remarks>
        /// This method is useful for the payload merge when parts of the
        /// payload are added little by little. Like this, less additional
        /// temporary buffer is necessary.
        /// </remarks>
        public void Write(ReadOnlySpan<byte> payload)
        {
            if (_offset + payload.Length > payload.Length)
                throw new ArgumentOutOfRangeException(nameof(payload),
                    "Trying to write over TxoPack max payloads capacity.");

            var destPayload = _buffer.Slice(_offset, payload.Length);
            payload.CopyTo(destPayload);
            _offset += payload.Length;
        }

        /// <summary>
        /// Write an BlockEvent into buffer.
        /// </summary>
        /// <remarks>
        /// This method is useful for the payload merge when events are added
        /// little by little. Like this, less additional temporary buffer is
        /// necessary.
        /// </remarks>
        public void Write(BlockEvent eventToWrite)
        {
            if (_offset + BlockEvent.SizeInBytes > _buffer.Length)
                throw new ArgumentOutOfRangeException($"Trying to write over buffer max payload capacity.");

            var destPayload = _buffer.Slice(_offset, BlockEvent.SizeInBytes);
            BitConverter.TryWriteBytes(destPayload, eventToWrite.Value);
            _offset += BlockEvent.SizeInBytes;
            ++_eventCount;
        }

        /// <summary>
        /// Write all events provided by input iterator
        /// </summary>
        /// <param name="iter">iterator if events</param>
        public void WriteRemainingEvents(ref PayloadEventIterator iter)
        {
            for (; iter.EndNotReached; iter.MoveNext())
            {
                Write(iter.Current);
            }
        }
    }
}
