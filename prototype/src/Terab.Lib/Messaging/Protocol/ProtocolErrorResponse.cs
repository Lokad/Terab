// Copyright Lokad 2018 under MIT BCH.
using System;
using System.Runtime.InteropServices;

namespace Terab.Lib.Messaging.Protocol
{
    public unsafe ref struct ProtocolErrorResponse
    {
        private readonly Span<byte> _buffer;

        public static int SizeInBytes => Header.SizeInBytes;

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct Header
        {
            public static readonly int SizeInBytes = sizeof(Header);

            public MessageHeader ResponseHeader;
            public ProtocolErrorStatus Status;
        }

        public ProtocolErrorResponse(Span<byte> buffer)
        {
            _buffer = buffer;
        }

        public ProtocolErrorResponse(
            Span<byte> buffer,
            RequestId requestId,
            ClientId clientId,
            ProtocolErrorStatus status)
        {
            _buffer = buffer;

            AsHeader.ResponseHeader.MessageSizeInBytes = Header.SizeInBytes;
            AsHeader.ResponseHeader.RequestId = requestId;
            AsHeader.ResponseHeader.ClientId = clientId;
            AsHeader.ResponseHeader.MessageKind = MessageKind.ProtocolErrorResponse;

            AsHeader.Status = status;
        }

        public ProtocolErrorResponse(
            RequestId requestId,
            ClientId clientId,
            ProtocolErrorStatus status,
            SpanPool<byte> pool)
        {
            _buffer = pool.GetSpan(Header.SizeInBytes);

            AsHeader.ResponseHeader.MessageSizeInBytes = Header.SizeInBytes;
            AsHeader.ResponseHeader.RequestId = requestId;
            AsHeader.ResponseHeader.ClientId = clientId;
            AsHeader.ResponseHeader.MessageKind = MessageKind.ProtocolErrorResponse;

            AsHeader.Status = status;
        }

        private ref Header AsHeader => ref MemoryMarshal.Cast<byte, Header>(_buffer)[0];

        public ref MessageHeader MessageHeader => ref AsHeader.ResponseHeader;

        public ProtocolErrorStatus Status => AsHeader.Status;

        public Span<byte> Span => _buffer.Slice(0, Header.SizeInBytes);
    }
}
