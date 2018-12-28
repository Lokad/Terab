// Copyright Lokad 2018 under MIT BCH.
using System;
using System.Runtime.InteropServices;

namespace Terab.Lib.Messaging.Protocol
{
    public unsafe ref struct CloseConnectionRequest
    {
        private readonly Span<byte> _buffer;

        public static int SizeInBytes => Header.SizeInBytes;

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct Header
        {
            public static readonly int SizeInBytes = sizeof(Header);

            public MessageHeader RequestHeader;
        }

        public CloseConnectionRequest(Span<byte> buffer, RequestId requestId, ClientId clientId)
        {
            _buffer = buffer;

            AsHeader.RequestHeader.MessageSizeInBytes = Header.SizeInBytes;
            AsHeader.RequestHeader.RequestId = requestId;
            AsHeader.RequestHeader.ClientId = clientId;
            AsHeader.RequestHeader.MessageKind = MessageKind.CloseConnection;
        }

        private ref Header AsHeader => ref MemoryMarshal.Cast<byte, Header>(_buffer)[0];

        public ref MessageHeader MessageHeader => ref AsHeader.RequestHeader;

        public Span<byte> Span => _buffer.Slice(0, Header.SizeInBytes);
    }
}
