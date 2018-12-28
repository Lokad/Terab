// Copyright Lokad 2018 under MIT BCH.
using System;
using System.Runtime.InteropServices;
using Terab.Lib.Chains;

namespace Terab.Lib.Messaging.Protocol
{
    public ref struct GetBlockHandleResponse
    {
        private Span<byte> _buffer;

        public static int SizeInBytes => Header.SizeInBytes;

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private unsafe struct Header
        {
            public static readonly int SizeInBytes = sizeof(Header);

            public MessageHeader ResponseHeader;
            public GetBlockHandleStatus Status;
            public BlockHandle Handle;
        }

        public GetBlockHandleResponse(Span<byte> buffer)
        {
            _buffer = buffer;
        }

        public GetBlockHandleResponse(
            ref MessageHeader requestHeader, BlockHandleMask mask, BlockAlias alias, SpanPool<byte> pool)
        {
            _buffer = pool.GetSpan(Header.SizeInBytes);

            AsHeader.ResponseHeader = requestHeader;

            AsHeader.ResponseHeader.MessageKind = MessageKind.BlockHandleResponse;
            AsHeader.ResponseHeader.MessageSizeInBytes = Header.SizeInBytes;

            AsHeader.Status = GetBlockHandleStatus.Success;
            AsHeader.Handle = alias.ConvertToBlockHandle(mask);
        }

        public GetBlockHandleResponse(
            ref MessageHeader requestHeader, GetBlockHandleStatus status, SpanPool<byte> pool)
        {
            _buffer = pool.GetSpan(Header.SizeInBytes);

            AsHeader.ResponseHeader = requestHeader;

            AsHeader.ResponseHeader.MessageKind = MessageKind.BlockHandleResponse;
            AsHeader.ResponseHeader.MessageSizeInBytes = Header.SizeInBytes;

            AsHeader.Status = status;
            AsHeader.Handle = default;
        }

        private ref Header AsHeader => ref MemoryMarshal.Cast<byte, Header>(_buffer)[0];

        public ref MessageHeader MessageHeader => ref AsHeader.ResponseHeader;

        public GetBlockHandleStatus Status => AsHeader.Status;

        public BlockHandle Handle => AsHeader.Handle;

        public Span<byte> Span => _buffer.Slice(0, Header.SizeInBytes);
    }
}