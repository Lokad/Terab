// Copyright Lokad 2018 under MIT BCH.
using System;
using System.Runtime.InteropServices;
using Terab.Lib.Chains;

namespace Terab.Lib.Messaging.Protocol
{
    public unsafe ref struct OpenBlockResponse
    {
        private readonly Span<byte> _buffer;

        public static int SizeInBytes = Header.SizeInBytes;

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct Header
        {
            public static readonly int SizeInBytes = sizeof(Header);

            public MessageHeader ResponseHeader;
            public OpenBlockStatus Status;
            public BlockHandle Handle;
            public UncommittedBlockId UncommittedBlockId;
        }

        public OpenBlockResponse(Span<byte> buffer)
        {
            _buffer = buffer;
        }

        public OpenBlockResponse(
            ref MessageHeader requestHeader, BlockHandleMask mask, BlockAlias alias, UncommittedBlockId uncommittedBlockId, SpanPool<byte> pool)
        {
            _buffer = pool.GetSpan(Header.SizeInBytes);

            AsHeader.ResponseHeader = requestHeader;

            AsHeader.ResponseHeader.MessageKind = MessageKind.OpenBlockResponse;
            AsHeader.ResponseHeader.MessageSizeInBytes = Header.SizeInBytes;

            AsHeader.Status = OpenBlockStatus.Success;
            AsHeader.Handle = alias.ConvertToBlockHandle(mask);
            AsHeader.UncommittedBlockId = uncommittedBlockId;
        }

        public OpenBlockResponse(ref MessageHeader requestHeader, OpenBlockStatus status, SpanPool<byte> pool)
        {
            _buffer = pool.GetSpan(Header.SizeInBytes);

            AsHeader.ResponseHeader = requestHeader;

            AsHeader.ResponseHeader.MessageKind = MessageKind.OpenBlockResponse;
            AsHeader.ResponseHeader.MessageSizeInBytes = Header.SizeInBytes;

            AsHeader.Status = status;
            AsHeader.Handle = default;
            AsHeader.UncommittedBlockId = default;
        }

        private ref Header AsHeader => ref MemoryMarshal.Cast<byte, Header>(_buffer)[0];

        public ref MessageHeader MessageHeader => ref AsHeader.ResponseHeader;

        public OpenBlockStatus Status => AsHeader.Status;

        public BlockHandle Handle => AsHeader.Handle;

        public UncommittedBlockId UncommittedBlockId => AsHeader.UncommittedBlockId;

        public Span<byte> Span => _buffer.Slice(0, Header.SizeInBytes);
    }
}