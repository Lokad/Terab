// Copyright Lokad 2018 under MIT BCH.
using System;
using System.Runtime.InteropServices;
using Terab.Lib.Chains;

namespace Terab.Lib.Messaging.Protocol
{
    public unsafe ref struct ConsumeCoinRequest
    {
        private Span<byte> _buffer;
        private readonly BlockHandleMask _mask;

        public static int SizeInBytes => Header.SizeInBytes;

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct Header
        {
            public static readonly int SizeInBytes = sizeof(Header);

            public MessageHeader RequestHeader;
            public Outpoint Outpoint;
            public BlockHandle Context;
        }

        /// <summary> Coin consumption. Client-side perspective. </summary>
        public ConsumeCoinRequest(
            RequestId requestId,
            ref Outpoint outpoint,
            BlockHandle context,
            SpanPool<byte> pool)
        {
            _buffer = pool.GetSpan(Header.SizeInBytes);
            _mask = default;

            AsHeader.RequestHeader.MessageSizeInBytes = Header.SizeInBytes;
            AsHeader.RequestHeader.RequestId = requestId;
            AsHeader.RequestHeader.MessageKind = MessageKind.ConsumeCoin;

            Outpoint = outpoint;
            HandleContext = context;
        }

        public ConsumeCoinRequest(Span<byte> buffer, BlockHandleMask mask)
        {
            _buffer = buffer;
            _mask = mask;
        }

        /// <summary>
        /// Allocate an array. Intended for testing purposes only.
        /// </summary>
        internal static ConsumeCoinRequest From(RequestId requestId,
            ref Outpoint outpoint,
            BlockAlias context,
            BlockHandleMask mask)
        {
            return new ConsumeCoinRequest(requestId, ref outpoint, 
                context.ConvertToBlockHandle(mask), new SpanPool<byte>(Header.SizeInBytes));
        }

        private ref Header AsHeader =>
            ref MemoryMarshal.Cast<byte, Header>(_buffer)[0];

        public ref MessageHeader MessageHeader => ref AsHeader.RequestHeader;

        public ref Outpoint Outpoint => ref AsHeader.Outpoint;

        public BlockAlias Context => AsHeader.Context.ConvertToBlockAlias(_mask);

        public BlockHandle HandleContext
        {
            get => AsHeader.Context;
            set => AsHeader.Context = value;
        }

        public BlockHandleMask Mask => _mask;

        public Span<byte> Span => _buffer.Slice(0, AsHeader.RequestHeader.MessageSizeInBytes);
    }
}