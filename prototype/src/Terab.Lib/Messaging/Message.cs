// Copyright Lokad 2018 under MIT BCH.
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Terab.Lib.Messaging
{
    /// <summary>
    /// All messages have in common the same 'MessageHeader'.
    /// </summary>
    [DebuggerDisplay("{Header.MessageKind}:{SizeInBytes}")]
    public ref struct Message
    {
        private readonly Span<byte> _buffer;

        public Message(Span<byte> buffer)
        {
            _buffer = buffer;
        }

        public ref MessageHeader Header
        {
            get => ref MemoryMarshal.Cast<byte, MessageHeader>(_buffer)[0];
        }

        public int SizeInBytes => Header.MessageSizeInBytes;

        public Span<byte> Span => _buffer.Slice(0, SizeInBytes);
    }
}