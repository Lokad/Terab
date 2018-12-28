// Copyright Lokad 2018 under MIT BCH.
using System;
using System.Threading;

namespace Terab.Lib.Messaging
{
    /// <summary>
    /// The bounded inbox supports multiple writers, but only a single reader.
    /// </summary >
    /// <remarks>
    /// Multiple producer threads can call <see cref = "TryWrite"/> to append
    /// binary messages to the inbox, which fails if there is not enough room
    /// left.
    ///
    /// A single consumer thread calls <see cref = "Peek"/> and
    /// <see cref = "Next()"/> to read from inbox.
    /// </remarks>
    public class BoundedInbox
    {
        public const int DefaultInboxSizeInBytes = 65536;

        /// <summary>
        /// Points towards the first place in the Inbox
        /// that has not yet been successfully treated by the reading thread.
        /// It is moved by the reading thread by the length of the last read
        /// message.
        /// </summary>
        private long _start;

        /// <summary>
        /// The first byte that has not been written to. 
        /// It is safe to read from <see cref="_start"/> up to this byte. 
        /// </summary>
        private long _end;

        /// <summary>
        /// The first byte that has not been written to AND is not currently
        /// being written to by a thread. 
        /// </summary>
        private long _write;

        /// <summary>
        /// Holds the <see cref="SizeInBytes"/> requested in the constructor
        /// plus an additional space in the end so that messages don't get cut 
        /// with one part of the message in the end of the array and 
        /// the other in the beginning.
        /// </summary>
        private readonly Memory<byte> _inbox;

        /// <summary>
        /// Official length of the Span.
        /// </summary>
        public readonly int SizeInBytes;


        /// <summary>
        /// Create an inbox that can store up to <see cref="sizeInBytes"/>
        /// amount of bytes.
        /// </summary>
        public BoundedInbox(int sizeInBytes = DefaultInboxSizeInBytes)
        {
            if (sizeInBytes < Constants.MaxResponseSize)
                throw new ArgumentException(
                    $"Size of Inbox {sizeInBytes} cannot be smaller than the maximum message length {Constants.MaxResponseSize}");

            _inbox = new byte[sizeInBytes + Constants.MaxResponseSize];
            SizeInBytes = sizeInBytes;
        }

        /// <summary>
        /// Read the next message of the Inbox
        /// without erasing the read data.
        /// </summary>
        public Memory<byte> Peek()
        {
            int startMod = (int)(_start % SizeInBytes);
            if (_start < _end)
            {
                var buffer = _inbox.Span.Slice(startMod);
                if (buffer.Length >= 4)
                {
                    var message = new Message(buffer);
                    return _inbox.Slice(startMod,  message.Header.MessageSizeInBytes);
                }
            }
            
            return Memory<byte>.Empty;
        }
        
        /// <summary>
        /// Determine if there is any message to peek.
        /// </summary>
        /// <remarks>
        /// Use this property before Peek to avoid Span.Empty.
        /// </remarks>
        public bool CanPeek => _start < _end;
      
        /// <summary>
        /// Allows reading thread to move to next message.
        /// </summary>
        public void Next()
        {
            _start += Peek().Length;
        }

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
            if(toWrite.Length < 4)
                throw new ArgumentException("Message shorter than 4 bytes.");

            var message = new Message(toWrite);
                
            // Verify the validity of the given message length.
            if (toWrite.Length != message.SizeInBytes)
                throw new ArgumentOutOfRangeException(
                    $"Message of size {message.SizeInBytes} in span of size {toWrite.Length}.");

            if (toWrite.Length > Constants.MaxResponseSize)
                throw new ArgumentOutOfRangeException(
                    $"Message size {message.SizeInBytes} exceeds {Constants.MaxResponseSize}.");

            // Reserve a space to write by moving `_write` forward by the expected
            // length. `write` remembers the start of the reserved space.

            long write;
            do
            {
                write = _write;
                if (SizeInBytes - (int) (write - _start) < toWrite.Length) return false;
            } while (write != Interlocked.CompareExchange(ref _write, write + toWrite.Length, write));

            try
            {
                // Write to the reserved space 
                var span = _inbox.Span;
                var start = Convert.ToInt32(write % SizeInBytes);
                for (var i = 0; i < toWrite.Length; ++i)
                    span[start + i] = toWrite[i];
                
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
