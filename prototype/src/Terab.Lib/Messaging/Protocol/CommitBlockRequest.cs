// Copyright Lokad 2018 under MIT BCH.
using System;
using System.Runtime.InteropServices;
using Terab.Lib.Chains;

namespace Terab.Lib.Messaging.Protocol
{
    public unsafe ref struct CommitBlockRequest
    {
        private Span<byte> _buffer;
        private readonly BlockHandleMask _mask;

        public static int SizeInBytes => Header.SizeInBytes;

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct Header
        {
            public static readonly int SizeInBytes = sizeof(Header);

            public MessageHeader RequestHeader;
            public BlockHandle Handle;
            public CommittedBlockId BlockId;
        }

        public CommitBlockRequest(Span<byte> buffer, BlockHandleMask mask)
        {
            _buffer = buffer;
            _mask = mask;
        }

        /// <summary>
        /// Allocate an array. Intended for testing purposes only.
        /// </summary>
        internal static CommitBlockRequest From(
            RequestId requestId, ClientId clientId, BlockHandle handle, CommittedBlockId blockId)
        {
            var request = new CommitBlockRequest { _buffer = new Span<byte>(new byte[sizeof(Header)]) };

            request.MessageHeader.MessageSizeInBytes = Header.SizeInBytes;
            request.MessageHeader.RequestId = requestId;
            request.MessageHeader.ClientId = clientId;
            request.MessageHeader.MessageKind = MessageKind.CommitBlock;

            request.AsHeader.Handle = handle;
            request.AsHeader.BlockId = blockId;

            return request;
        }

        private ref Header AsHeader => ref MemoryMarshal.Cast<byte, Header>(_buffer)[0];

        public ref MessageHeader MessageHeader => ref AsHeader.RequestHeader;

        public BlockAlias BlockAlias => AsHeader.Handle.ConvertToBlockAlias(_mask);

        /// <summary> Intended for debugging purposes. </summary>
        public BlockHandle Handle => AsHeader.Handle;

        public ref CommittedBlockId BlockId => ref AsHeader.BlockId;

        public BlockHandleMask Mask => _mask;

        public Span<byte> Span => _buffer.Slice(0, Header.SizeInBytes);
    }
}