// Copyright Lokad 2018 under MIT BCH.
using System;
using System.Runtime.InteropServices;

namespace Terab.Lib.Messaging.Protocol
{
    public unsafe ref struct OpenBlockRequest
    {
        private Span<byte> _buffer;
        private readonly BlockHandleMask _mask;

        public static int SizeInBytes => Header.SizeInBytes;

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct Header
        {
            public static readonly int SizeInBytes = sizeof(Header);

            public MessageHeader RequestHeader;
            public CommittedBlockId ParentId;
        }

        public OpenBlockRequest(Span<byte> buffer, BlockHandleMask mask)
        {
            _buffer = buffer;
            _mask = mask;
        }

        /// <summary>
        /// Allocate an array. Intended for testing purposes only.
        /// </summary>
        internal static OpenBlockRequest ForGenesis(RequestId requestId)
        {
            var request = new OpenBlockRequest { _buffer = new Span<byte>(new byte[sizeof(Header)]) };

            request.MessageHeader.MessageSizeInBytes = Header.SizeInBytes;
            request.MessageHeader.RequestId = requestId;
            request.MessageHeader.ClientId = default;
            request.MessageHeader.MessageKind = MessageKind.OpenBlock;

            request.AsHeader.ParentId = default;

            return request;
        }

        /// <summary>
        /// Allocate an array. Intended for testing purposes only.
        /// </summary>
        internal static OpenBlockRequest From(
            RequestId requestId, ClientId clientId, CommittedBlockId parentId)
        {
            var request = new OpenBlockRequest { _buffer = new Span<byte>(new byte[sizeof(Header)]) };

            request.MessageHeader.MessageSizeInBytes = Header.SizeInBytes;
            request.MessageHeader.RequestId = requestId;
            request.MessageHeader.ClientId = clientId;
            request.MessageHeader.MessageKind = MessageKind.OpenBlock;

            request.AsHeader.ParentId = parentId;

            return request;
        }

        private ref Header AsHeader => ref MemoryMarshal.Cast<byte, Header>(_buffer)[0];

        public ref MessageHeader MessageHeader => ref AsHeader.RequestHeader;

        public CommittedBlockId ParentId => AsHeader.ParentId;

        public BlockHandleMask Mask => _mask;

        public Span<byte> Span => _buffer.Slice(0, Header.SizeInBytes);
    }
}