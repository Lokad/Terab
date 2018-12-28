// Copyright Lokad 2018 under MIT BCH.
using System;
using System.Runtime.InteropServices;
using Terab.Lib.Chains;

namespace Terab.Lib.Messaging.Protocol
{
    public unsafe ref struct GetCoinResponse
    {
        private Span<byte> _buffer;

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct Header
        {
            public static readonly int SizeInBytes = sizeof(Header);

            public MessageHeader ResponseHeader;
            public GetCoinStatus Status;
            public Outpoint Outpoint;
            public OutpointFlags Flags;
            public BlockHandle Context;
            public BlockHandle Production;
            public BlockHandle Consumption;
            public ulong Satoshis;
            public uint NLockTime;
        }

        public GetCoinResponse(Span<byte> buffer)
        {
            _buffer = buffer;
        }

        public GetCoinResponse(
            RequestId requestId,
            ClientId clientId,
            GetCoinStatus status,
            ref Outpoint outpoint,
            OutpointFlags flags,
            BlockAlias context,
            BlockAlias production,
            BlockAlias consumption,
            ulong satoshis,
            uint nLockTime,
            Span<byte> script,
            BlockHandleMask mask,
            SpanPool<byte> pool)
        {
            var messageSizeInBytes = Header.SizeInBytes + script.Length;
            _buffer = pool.GetSpan(messageSizeInBytes);

            AsHeader.ResponseHeader.MessageSizeInBytes = messageSizeInBytes;
            AsHeader.ResponseHeader.RequestId = requestId;
            AsHeader.ResponseHeader.ClientId = clientId;
            AsHeader.ResponseHeader.MessageKind = MessageKind.GetCoinResponse;

            AsHeader.Status = status;
            AsHeader.Outpoint = outpoint;
            AsHeader.Flags = flags;
            AsHeader.Context = context.ConvertToBlockHandle(mask);
            AsHeader.Production = production.ConvertToBlockHandle(mask);
            AsHeader.Consumption = consumption.ConvertToBlockHandle(mask);
            AsHeader.Satoshis = satoshis;
            AsHeader.NLockTime = nLockTime;

            script.CopyTo(_buffer.Slice(Header.SizeInBytes));
        }

        private ref Header AsHeader => ref MemoryMarshal.Cast<byte, Header>(_buffer)[0];

        public ref MessageHeader MessageHeader => ref AsHeader.ResponseHeader;

        public GetCoinStatus Status => AsHeader.Status;

        public ref Outpoint Outpoint => ref AsHeader.Outpoint;

        public BlockHandle Production => AsHeader.Production;

        public BlockHandle Consumption => AsHeader.Consumption;

        public BlockHandle Context => AsHeader.Context;

        public OutpointFlags OutpointFlags => AsHeader.Flags;

        public uint NLockTime => AsHeader.NLockTime;

        public ulong Satoshis => AsHeader.Satoshis;

        public Span<byte> Script => _buffer.Slice(Header.SizeInBytes,
            AsHeader.ResponseHeader.MessageSizeInBytes - Header.SizeInBytes);

        public Span<byte> Span => _buffer.Slice(0, AsHeader.ResponseHeader.MessageSizeInBytes);
    }
}