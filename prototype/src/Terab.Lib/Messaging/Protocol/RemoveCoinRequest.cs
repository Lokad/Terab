// Copyright Lokad 2018 under MIT BCH.
using System;
using System.Runtime.InteropServices;
using Terab.Lib.Chains;

namespace Terab.Lib.Messaging.Protocol
{
    public unsafe ref struct RemoveCoinRequest
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
            public bool RemoveProduction;
            public bool RemoveConsumption;
        }

        /// <summary> Coin removal. Client-side perspective. </summary>
        public RemoveCoinRequest(
            Span<byte> buffer,
            RequestId requestId,
            ref Outpoint outpoint,
            BlockHandle context,
            bool removeProduction,
            bool removeConsumption)
        {
            _buffer = buffer;
            _mask = default;

            AsHeader.RequestHeader.MessageSizeInBytes = Header.SizeInBytes;
            AsHeader.RequestHeader.RequestId = requestId;
            AsHeader.RequestHeader.MessageKind = MessageKind.RemoveCoin;

            Outpoint = outpoint;
            HandleContext = context;
            RemoveProduction = removeProduction;
            RemoveConsumption = removeConsumption;
        }

        public RemoveCoinRequest(Span<byte> buffer, BlockHandleMask mask)
        {
            _buffer = buffer;
            _mask = mask;
        }

        private ref Header AsHeader =>
            ref MemoryMarshal.Cast<byte, Header>(_buffer)[0];

        public ref MessageHeader MessageHeader => ref AsHeader.RequestHeader;

        public ref Outpoint Outpoint => ref AsHeader.Outpoint;

        public bool RemoveProduction
        {
            get => AsHeader.RemoveProduction;
            set => AsHeader.RemoveProduction = value;
        }

        public bool RemoveConsumption
        {
            get => AsHeader.RemoveConsumption;
            set => AsHeader.RemoveConsumption = value;
        }

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