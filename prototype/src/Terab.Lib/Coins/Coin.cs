// Copyright Lokad 2018 under MIT BCH.
using System;
using System.Runtime.InteropServices;
using Terab.Lib.Messaging;

namespace Terab.Lib.Coins
{
    public ref struct Coin
    {
        /// <remarks>
        /// The buffer can exceed the content of the coin.
        /// </remarks>
        private Span<byte> _buffer;

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private unsafe struct CoinHeader
        {
            public readonly static int SizeInBytes = sizeof(CoinHeader);

            // Outpoint
            public Outpoint Outpoint;
            public TerabOutpointFlags Flags;

            // Counters
            public ushort EventCount;
            public ushort PayloadLengthInBytes;
        }

        private ref CoinHeader AsHeader => ref MemoryMarshal.Cast<byte, CoinHeader>(_buffer)[0];

        public int SizeInBytes
        {
            get
            {
                var header = AsHeader;
                return CoinHeader.SizeInBytes 
                       + header.EventCount * CoinEvent.SizeInBytes
                       + header.PayloadLengthInBytes;
            }
        }

        public Span<byte> Span => _buffer.Slice(0, SizeInBytes);

        public bool IsEmpty => _buffer.IsEmpty;

        public static Coin Empty => new Coin(Span<byte>.Empty);

        public ref Outpoint Outpoint => ref AsHeader.Outpoint;

        public TerabOutpointFlags Flags => AsHeader.Flags;

        /// <remarks>'ref struct' properties do not admit setters in C# 7.0.</remarks>
        public void SetOutpoint(ref Outpoint outpoint)
        {
            AsHeader.Outpoint = outpoint;
        }

        public TerabOutpointFlags OutpointFlags
        {
            get => AsHeader.Flags;
            set => AsHeader.Flags = value;
        }

        public Span<CoinEvent> Events => MemoryMarshal.Cast<byte, CoinEvent>(
            _buffer.Slice(CoinHeader.SizeInBytes, CoinEvent.SizeInBytes * AsHeader.EventCount));

        public Payload Payload => new Payload(_buffer.Slice(PayloadOffset));

        public Coin(Span<byte> buffer)
        {
            _buffer = buffer;
        }

        public Coin(ref Outpoint outpoint, bool isCoinBase, Payload payload, CoinEvent evt, SpanPool<byte> pool)
        {
            _buffer = pool.GetSpan(CoinHeader.SizeInBytes + CoinEvent.SizeInBytes + payload.SizeInBytes);

            SetOutpoint(ref outpoint);
            OutpointFlags = isCoinBase ? TerabOutpointFlags.PersistentIsCoinbase : TerabOutpointFlags.None;

            var events = MemoryMarshal.Cast<byte, CoinEvent>(_buffer.Slice(CoinHeader.SizeInBytes));
            events[0] = evt;
            AsHeader.EventCount = 1;

            SetPayload(payload);
        }

        public static Coin WithExtraEvent(Coin original, CoinEvent toAdd, SpanPool<byte> pool)
        {
            var extendedCoin = new Coin(pool.GetSpan(original.SizeInBytes + CoinEvent.SizeInBytes));
            original.Span.CopyTo(extendedCoin._buffer);

            var events = MemoryMarshal.Cast<byte, CoinEvent>(extendedCoin._buffer.Slice(CoinHeader.SizeInBytes));
            var eventCount = extendedCoin.AsHeader.EventCount;

            events[eventCount] = toAdd;
            extendedCoin.AsHeader.EventCount += 1;
            extendedCoin.SetPayload(original.Payload);

            return extendedCoin;
        }

        public static Coin WithLessEvent(Coin original, CoinEvent toRemove, SpanPool<byte> pool)
        {
            var shortenedCoin = new Coin(pool.GetSpan(original.SizeInBytes - CoinEvent.SizeInBytes));
            original.Span.Slice(0, original.SizeInBytes - CoinEvent.SizeInBytes).CopyTo(shortenedCoin._buffer);

            var shortenedEvents = shortenedCoin.Events;
            ushort eventIndex = 0;
            foreach (var ev in original.Events)
            {
                if (!ev.Equals(toRemove))
                {
                    shortenedEvents[eventIndex++] = ev;
                }
            }

            shortenedCoin.AsHeader.EventCount = eventIndex;
            shortenedCoin.SetPayload(original.Payload);

            return shortenedCoin;
        }

        public bool HasEvent(CoinEvent evt)
        {
            foreach (var ev in Events)
            {
                if (ev.Equals(evt))
                    return true;
            }

            return false;
        }

        /// <summary> Must be called before 'SetPayload'. </summary>
        public void SetEvents(ReadOnlySpan<CoinEvent> events)
        {
            if(events.Length >= ushort.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(events));

            if(AsHeader.PayloadLengthInBytes > 0)
                throw new InvalidOperationException();

            var span = MemoryMarshal.Cast<byte, CoinEvent>(_buffer.Slice(CoinHeader.SizeInBytes));
            events.CopyTo(span);
            AsHeader.EventCount = (ushort)events.Length;
        }

        public void SetPayload(Payload payload)
        {
            var span = _buffer.Slice(PayloadOffset);
            payload.Span.CopyTo(span);

            AsHeader.PayloadLengthInBytes = (ushort)payload.SizeInBytes;
        }

        private int PayloadOffset => CoinHeader.SizeInBytes + AsHeader.EventCount * CoinEvent.SizeInBytes;
    }
}
