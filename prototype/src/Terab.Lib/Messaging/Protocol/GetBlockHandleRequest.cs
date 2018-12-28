// Copyright Lokad 2018 under MIT BCH.
using System;
using System.Runtime.InteropServices;

namespace Terab.Lib.Messaging.Protocol
{
    /// <summary>
    /// Request the block handle of a committed block or (exclusive)
    /// of an uncommitted block.
    /// </summary>
    public unsafe ref struct GetBlockHandleRequest
    {
        private Span<byte> _buffer;
        private readonly BlockHandleMask _mask;

        public static int SizeInBytes => Header.SizeInBytes;

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct Header
        {
            public static int SizeInBytes => sizeof(Header);

            public MessageHeader RequestHeader;
            public CommittedBlockId CommittedBlockId;
            public UncommittedBlockId UncommittedBlockId;
            public bool IsCommitted;
        }

        public GetBlockHandleRequest(Span<byte> buffer, BlockHandleMask mask)
        {
            _buffer = buffer;
            _mask = mask;
        }

        /// <summary>
        /// Allocate an array. Intended for testing purposes only.
        /// </summary>
        internal static GetBlockHandleRequest From(RequestId requestId, ClientId clientId, CommittedBlockId blockId)
        {
            var request = new GetBlockHandleRequest {_buffer = new Span<byte>(new byte[Header.SizeInBytes])};

            request.MessageHeader.MessageSizeInBytes = Header.SizeInBytes;
            request.MessageHeader.RequestId = requestId;
            request.MessageHeader.ClientId = clientId;
            request.MessageHeader.MessageKind = MessageKind.GetBlockHandle;

            request.AsHeader.CommittedBlockId = blockId;
            request.AsHeader.IsCommitted = true;

            return request;
        }

        /// <summary>
        /// Allocate an array. Intended for testing purposes only.
        /// </summary>
        internal static GetBlockHandleRequest From(RequestId requestId, ClientId clientId,
            UncommittedBlockId uncommittedBlockId)
        {
            var request = new GetBlockHandleRequest {_buffer = new Span<byte>(new byte[Header.SizeInBytes])};

            request.MessageHeader.MessageSizeInBytes = Header.SizeInBytes;
            request.MessageHeader.RequestId = requestId;
            request.MessageHeader.ClientId = clientId;
            request.MessageHeader.MessageKind = MessageKind.GetBlockHandle;

            request.AsHeader.UncommittedBlockId = uncommittedBlockId;
            request.AsHeader.IsCommitted = false;

            return request;
        }

        private ref Header AsHeader => ref MemoryMarshal.Cast<byte, Header>(_buffer)[0];

        public ref MessageHeader MessageHeader => ref AsHeader.RequestHeader;

        /// <summary> Ignored if 'IsCommitted' is false. </summary>
        public ref CommittedBlockId CommittedBlockId => ref AsHeader.CommittedBlockId;

        /// <summary> Ignored if 'IsCommitted' is true. </summary>
        public ref UncommittedBlockId UncommittedBlockId => ref AsHeader.UncommittedBlockId;

        public bool IsCommitted => AsHeader.IsCommitted;

        public BlockHandleMask Mask => _mask;

        public Span<byte> Span => _buffer.Slice(0, Header.SizeInBytes);
    }
}