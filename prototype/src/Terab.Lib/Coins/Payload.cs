// Copyright Lokad 2018 under MIT BCH.
using System;
using System.Runtime.InteropServices;

namespace Terab.Lib.Coins
{
    /// <summary>Bitcoin-invariant payload associated to an outpoint.</summary>
    public unsafe ref struct Payload
    {
        /// <remarks>
        /// The buffer can exceed the content of the payload.
        /// </remarks>
        private Span<byte> _buffer;

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct PayloadHeader
        {
            public readonly static int SizeInBytes = sizeof(PayloadHeader);

            public ulong Satoshis;
            public uint NLockTime;
            public ushort ScriptLengthInBytes;
        }

        private ref PayloadHeader AsHeader => ref MemoryMarshal.Cast<byte, PayloadHeader>(_buffer)[0];

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

        public ushort ScriptLengthInBytes => AsHeader.ScriptLengthInBytes;

        public Span<byte> Script => _buffer.Slice(PayloadHeader.SizeInBytes, AsHeader.ScriptLengthInBytes);

        public int SizeInBytes => PayloadHeader.SizeInBytes + AsHeader.ScriptLengthInBytes;

        public Span<byte> Span => _buffer.Slice(0, SizeInBytes);

        public Payload(Span<byte> buffer)
        {
            _buffer = buffer;
        }

        public Payload(ulong satoshis, uint nLockTime, Span<byte> script, SpanPool<byte> pool)
        {
            _buffer = pool.GetSpan(PayloadHeader.SizeInBytes + script.Length);

            Satoshis = satoshis;
            NLockTime = nLockTime;
            Append(script);
        }

        public void Append(ReadOnlySpan<byte> script)
        {
            if (script.Length >= ushort.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(script));

            AsHeader.ScriptLengthInBytes = (ushort)script.Length;

            var span = _buffer.Slice(PayloadHeader.SizeInBytes);
            script.CopyTo(span);
        }
    }
}

