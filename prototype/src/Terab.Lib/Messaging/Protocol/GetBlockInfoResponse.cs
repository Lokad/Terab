// Copyright Lokad 2018 under MIT BCH.
using System;
using System.Runtime.InteropServices;
using Terab.Lib.Chains;

namespace Terab.Lib.Messaging.Protocol
{
    public unsafe ref struct GetBlockInfoResponse
    {
        private readonly Span<byte> _buffer;

        public static int SizeInBytes => Header.SizeInBytes;

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct Header
        {
            public static readonly int SizeInBytes = sizeof(Header);

            public MessageHeader ResponseHeader;
            public CommittedBlockId CommittedBlockId;
            public UncommittedBlockId UncommittedBlockId;
            public BlockHandle Handle;
            public BlockHandle Parent;
            public int BlockHeight;
            public bool IsCommitted;
        }

        public GetBlockInfoResponse(Span<byte> buffer)
        {
            _buffer = buffer;
        }

        public GetBlockInfoResponse(
            ref MessageHeader requestHeader, 
            CommittedBlockId committedBlockId,
            BlockAlias alias,
            BlockAlias parent,
            int blockHeight,
            BlockHandleMask mask,
            SpanPool<byte> pool)
        {
            _buffer = pool.GetSpan(Header.SizeInBytes);

            AsHeader.ResponseHeader = requestHeader;
            AsHeader.ResponseHeader.MessageKind = MessageKind.BlockInfoResponse;
            AsHeader.ResponseHeader.MessageSizeInBytes = Header.SizeInBytes;

            AsHeader.CommittedBlockId = committedBlockId;
            AsHeader.Handle = alias.ConvertToBlockHandle(mask);
            AsHeader.Parent = parent.ConvertToBlockHandle(mask);
            AsHeader.BlockHeight = blockHeight;
            AsHeader.IsCommitted = true;
        }

        public GetBlockInfoResponse(
            ref MessageHeader requestHeader,
            UncommittedBlockId uncommittedBlockId,
            BlockAlias alias,
            BlockAlias parent,
            int blockHeight,
            BlockHandleMask mask,
            SpanPool<byte> pool)
        {
            _buffer = pool.GetSpan(Header.SizeInBytes);

            AsHeader.ResponseHeader = requestHeader;
            AsHeader.ResponseHeader.MessageKind = MessageKind.BlockInfoResponse;
            AsHeader.ResponseHeader.MessageSizeInBytes = Header.SizeInBytes;

            AsHeader.UncommittedBlockId = uncommittedBlockId;
            AsHeader.Handle = alias.ConvertToBlockHandle(mask);
            AsHeader.Parent = parent.ConvertToBlockHandle(mask);
            AsHeader.BlockHeight = blockHeight;
            AsHeader.IsCommitted = false;
        }

        private ref Header AsHeader => ref MemoryMarshal.Cast<byte, Header>(_buffer)[0];

        public ref MessageHeader MessageHeader => ref AsHeader.ResponseHeader;

        /// <summary> Defined only if 'IsCommitted' is true. </summary>
        public ref CommittedBlockId CommittedBlockId => ref AsHeader.CommittedBlockId;

        /// <summary> Defined only if 'IsCommitted' is false. </summary>
        public ref UncommittedBlockId UncommittedBlockId => ref AsHeader.UncommittedBlockId;

        public bool IsCommitted => AsHeader.IsCommitted;

        public BlockHandle Handle => AsHeader.Handle;

        public BlockHandle Parent => AsHeader.Parent;

        public int BlockHeight => AsHeader.BlockHeight;

        public Span<byte> Span => _buffer.Slice(0, Header.SizeInBytes);
    }
}