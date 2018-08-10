using System;
using Terab.UTXO.Core.Blockchain;

namespace Terab.UTXO.Core
{
    /// <summary>
    /// Iterator struct to move forward inside a BlockEvent array.
    /// <see cref="Terab.UTXO.Core.Blockchain.BlockEvent"/>
    /// </summary>
    public ref struct PayloadEventIterator
    {
        private readonly ReadOnlySpan<byte> _buffer;

        private int _offset;


        public PayloadEventIterator(ReadOnlySpan<byte> buffer)
        {
            _buffer = buffer;
            _offset = 0;
        }

        /// <summary>
        /// Get current event
        /// </summary>
        public BlockEvent Current
        {
            get
            {
                if (!EndNotReached)
                    throw new IndexOutOfRangeException("PayloadEventIterator index overflow.");

                var number = BitConverter.ToUInt32(_buffer.Slice(_offset, BlockEvent.SizeInBytes));
                return new BlockEvent(number);
            }
        }

        /// <summary>
        /// Move the offset to potential next event
        /// </summary>
        /// <returns></returns>
        public void MoveNext()
        {
            _offset += BlockEvent.SizeInBytes;
        }

        /// <summary>
        /// Indicate that iterator hasn't reached the end.
        /// In other words, we can still get a current element
        /// </summary>
        public bool EndNotReached => _offset < _buffer.Length;
    }
}