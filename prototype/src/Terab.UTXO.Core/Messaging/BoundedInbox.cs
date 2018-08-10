using System;
using System.Threading;

namespace Terab.UTXO.Core.Messaging
{
    /// <summary>
    /// Thread-safe queue that stores a fixed amount of binary messages.
    /// </summary >
    /// <remarks>
    /// A single producer thread calls <see cref = "TryWrite"/> to append binary
    /// messages to the inbox, which fails if there is not enough room left.
    ///
    /// A single consumer thread calls <see cref = "Peek"/> and
    /// <see cref = "Next()"/> to read from inbox.
    /// </remarks>
    public class BoundedInbox
    {
        /// <summary>
        /// This pointer points towards the first place in the Inbox
        /// that has not yet been successfully treated by the reading thread.
        /// It is moved by the reading thread by the length of the last read
        /// message, once this has successfully been treated.
        /// </summary>
        private long _start;

        /// <summary>
        /// The first byte in the Inbox that has not been written to. 
        /// It is safe to read from <see cref="_start"/> up to this byte. 
        /// </summary>
        private long _end;

        /// <summary>
        /// The first byte that has not been written to AND is not currently
        /// being written to by a thread. 
        /// </summary>
        private long _write;

        /// <summary>
        /// Holds the <see cref="_sizeInBytes"/> requested in the constructor
        /// plus an additional space in the end so that messages don't get cut 
        /// in half with one part of the message in the end of the array and 
        /// the other in the beginning.
        /// </summary>
        private readonly byte[] _inbox;

        /// <summary>
        /// This is the official length of the Span.
        /// </summary>
        private readonly int _sizeInBytes;

        /// <summary>
        /// Create a BoundedInbox with capacity of at least a single message.
        /// Normally one uses this factory method instead of constructor
        /// </summary>
        public static BoundedInbox Create()
        {
            return new BoundedInbox(ClientServerMessage.MaxSizeInBytes);
        }


        /// <summary>
        /// Create an inbox that can store up to this amount of bytes.
        /// </summary>
        public BoundedInbox(int sizeInBytes)
        {
            if (sizeInBytes < ClientServerMessage.MaxSizeInBytes)
                throw new ArgumentException(
                    $"Size of Inbox {sizeInBytes} cannot be smaller than the maximum message length {ClientServerMessage.MaxSizeInBytes}");
            _inbox = new byte[sizeInBytes + ClientServerMessage.MaxSizeInBytes];
            _sizeInBytes = sizeInBytes;
        }

        /// <summary>
        /// Read the next message of the Inbox
        /// without erasing the read data.
        /// </summary>
        public Span<byte> Peek()
        {
            int startMod = (int)(_start % _sizeInBytes);
            if (_start < _end)
            {
                if(ClientServerMessage.TryGetLength(new Span<byte>(_inbox).Slice(startMod), out var bufferSize))
                    return new Span<byte>(_inbox, startMod, bufferSize);
            }

            return new Span<byte>(_inbox, startMod, 0);
        }

        /// <summary>
        /// Allows reading thread to read next message.
        /// </summary>
        public void Next()
        {
            Next(Peek().Length);
        }

        /// <summary>
        /// Allows reading thread to read next message.
        /// </summary>
        /// <param name="offset">offset to be applied on start position</param>
        public void Next(int offset)
        {
            _start += offset;
        }

        /// <summary>
        /// Allows writing thread to write next message.
        /// Can be called simultaneously from multiple threads.
        /// </summary>
        /// <returns>
        /// False if there was not enough room in the inbox to write
        /// the message. 
        /// </returns>
        public bool TryWrite(Span<byte> toWrite)
        {
            if (!ClientServerMessage.TryGetLength(toWrite, out var messageSize))
                throw new ArgumentException("Message shorter than 4 bytes.");

            // Verify the validity of the given message length.
            if (toWrite.Length != messageSize)
                throw new ArgumentOutOfRangeException(
                    $"Message of size {messageSize} in span of size {toWrite.Length}.");

            if (toWrite.Length > ClientServerMessage.MaxSizeInBytes)
                throw new ArgumentOutOfRangeException(
                    $"Message size {messageSize} exceeds {ClientServerMessage.MaxSizeInBytes}.");

            // Reserve a space to write by moving `_write` forward by the expected
            // length. `write` remembers the start of the reserved space.

            long write;
            do
            {
                write = _write;
                if (_sizeInBytes - (int) (write - _start) < toWrite.Length) return false;
            } while (write != Interlocked.CompareExchange(ref _write, write + toWrite.Length, write));

            try
            {
                // Write to the reserved space 

                var start = write % _sizeInBytes;
                for (var i = 0; i < toWrite.Length; ++i)
                    _inbox[start + i] = toWrite[i];
                
                return true;
            }
            finally
            {
                // Wait until we can move `_end` forward (previously pending writes must finish
                // first, or we'll expose readers to incomplete messages).            

                while (_end <= write)
                    Interlocked.CompareExchange(ref _end, write + toWrite.Length, write);
            }
        }
    }
}
