// Copyright Lokad 2018 under MIT BCH.
using System;
using System.Runtime.InteropServices;
using Terab.Lib.Chains;

namespace Terab.Lib.Messaging.Protocol
{
    public unsafe ref struct ProduceCoinRequest
    {
        private Span<byte> _buffer;
        private readonly BlockHandleMask _mask;

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct Header
        {
            public static readonly int SizeInBytes = sizeof(Header);

            public MessageHeader RequestHeader;
            public Outpoint Outpoint;
            public BlockHandle Context;
            public OutpointFlags Flags;
            public ulong Satoshis;
            public uint NLockTime;
        }

        /// <summary> Coin production. Client-side perspective. </summary>
        public ProduceCoinRequest(
            RequestId requestId,
            ref Outpoint outpoint,
            OutpointFlags flags,
            BlockHandle context,
            ulong satoshis,
            uint nLockTime,
            int scriptSizeInBytes,
            SpanPool<byte> pool)
        {
            _buffer = pool.GetSpan(Header.SizeInBytes + scriptSizeInBytes);
            _mask = default;

            AsHeader.RequestHeader.MessageSizeInBytes = Header.SizeInBytes + scriptSizeInBytes;
            AsHeader.RequestHeader.RequestId = requestId;
            AsHeader.RequestHeader.MessageKind = MessageKind.ProduceCoin;

            Outpoint = outpoint;
            HandleContext = context;
            Flags = flags;
            Satoshis = satoshis;
            NLockTime = nLockTime;
        }

        public ProduceCoinRequest(Span<byte> buffer, BlockHandleMask mask)
        {
            _buffer = buffer;
            _mask = mask;
        }

        /// <summary>
        /// Allocate an array. Intended for testing purposes only.
        /// </summary>
        internal static ProduceCoinRequest From(
            RequestId requestId,
            ClientId clientId,
            Outpoint outpoint,
            OutpointFlags flags,
            BlockAlias context,
            ulong satoshis,
            uint nLockTime,
            Span<byte> script,
            BlockHandleMask mask)
        {
            var request = new ProduceCoinRequest
                {_buffer = new Span<byte>(new byte[Header.SizeInBytes + script.Length])};

            request.MessageHeader.MessageSizeInBytes = Header.SizeInBytes + script.Length;
            request.MessageHeader.RequestId = requestId;
            request.MessageHeader.ClientId = clientId;
            request.MessageHeader.MessageKind = MessageKind.ProduceCoin;

            request.AsHeader.Outpoint = outpoint;
            request.AsHeader.Flags = flags;
            request.AsHeader.Context = context.ConvertToBlockHandle(mask);
            request.AsHeader.Satoshis = satoshis;
            request.AsHeader.NLockTime = nLockTime;

            script.CopyTo(request._buffer.Slice(Header.SizeInBytes));

            return request;
        }

        private ref Header AsHeader =>
            ref MemoryMarshal.Cast<byte, Header>(_buffer)[0];

        public ref MessageHeader MessageHeader => ref AsHeader.RequestHeader;

        public ref Outpoint Outpoint => ref AsHeader.Outpoint;

        public OutpointFlags Flags
        {
            get => AsHeader.Flags;
            set => AsHeader.Flags = value;
        }

        public BlockAlias Context => AsHeader.Context.ConvertToBlockAlias(_mask);

        public BlockHandle HandleContext
        {
            get => AsHeader.Context;
            set => AsHeader.Context = value;
        }

        public ulong Satoshis
        {
            get => AsHeader.Satoshis;
            set => AsHeader.Satoshis = value;
        }

        public uint NLockTime
        {
            get => AsHeader.NLockTime;
            set => AsHeader.NLockTime = value;
        }

        public Span<byte> Script => _buffer.Slice(Header.SizeInBytes,
            AsHeader.RequestHeader.MessageSizeInBytes - Header.SizeInBytes);

        public BlockHandleMask Mask => _mask;

        public Span<byte> Span => _buffer.Slice(0, AsHeader.RequestHeader.MessageSizeInBytes);
    }
}