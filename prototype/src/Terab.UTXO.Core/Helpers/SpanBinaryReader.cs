using System;
using System.IO;

namespace Terab.UTXO.Core.Helpers
{
    /// <summary>
    /// Implements the combined functionality of a BinaryReader over a MemoryStream;
    /// but this time  over a <see cref="Span{Byte}"/>, instead of a <c>byte[]</c>.
    /// </summary>
    /// <remarks>
    /// There are additional methods, too, such as <see cref="SkipInt32"/>
    /// </remarks>
    public ref struct SpanBinaryReader
    {
        private ReadOnlySpan<byte> _remaining;
        private readonly ReadOnlySpan<byte> _buffer;

        public SpanBinaryReader(ReadOnlySpan<byte> buffer)
        {
            _remaining = buffer;
            _buffer = buffer;
        }

        public Int64 ReadInt64()
        {
            var result = System.Buffers.Binary.BinaryPrimitives.ReadInt64LittleEndian(_remaining);
            _remaining = _remaining.Slice(sizeof(Int64));
            return result;
        }
        public Int64 ReadInt64BE()
        {
            var result = System.Buffers.Binary.BinaryPrimitives.ReadInt64BigEndian(_remaining);
            _remaining = _remaining.Slice(sizeof(Int64));
            return result;
        }

        public UInt64 ReadUInt64()
        {
            var result = System.Buffers.Binary.BinaryPrimitives.ReadUInt64LittleEndian(_remaining);
            _remaining = _remaining.Slice(sizeof(UInt64));
            return result;
        }

        public UInt64 ReadUInt64BE()
        {
            var result = System.Buffers.Binary.BinaryPrimitives.ReadUInt64BigEndian(_remaining);
            _remaining = _remaining.Slice(sizeof(UInt64));
            return result;
        }

        public Int32 ReadInt32()
        {
            var result = System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(_remaining);
            _remaining = _remaining.Slice(sizeof(Int32));
            return result;
        }

        public UInt32 ReadUInt32()
        {
            var result = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(_remaining);
            _remaining = _remaining.Slice(sizeof(Int32));
            return result;
        }

        public Int16 ReadInt16()
        {
            var result = System.Buffers.Binary.BinaryPrimitives.ReadInt16LittleEndian(_remaining);
            _remaining = _remaining.Slice(sizeof(Int16));
            return result;
        }

        public UInt16 ReadUInt16()
        {
            var result = System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(_remaining);
            _remaining = _remaining.Slice(sizeof(Int16));
            return result;
        }

        public SByte ReadSbyte()
        {
            var result = unchecked((sbyte) _remaining[0]);
            _remaining = _remaining.Slice(1);
            return result;
        }

        public Byte ReadByte()
        {
            var result = _remaining[0];
            _remaining = _remaining.Slice(1);
            return result;
        }

        public Boolean ReadBoolean() => ReadByte() != 0;

        public int Length => _buffer.Length;
        public int RemainingLength => _remaining.Length;
        public int Position => _buffer.Length - _remaining.Length;

        public void Seek(int n, SeekOrigin origin)
        {
            switch (origin)
            {
                case SeekOrigin.Begin:
                    _remaining = _buffer.Slice(n);
                    break;
                case SeekOrigin.End:
                    _remaining = _buffer.Slice(_buffer.Length - n);
                    break;
                case SeekOrigin.Current:
                    if (n >= 0)
                    {
                        _remaining = _remaining.Slice(n);
                    }
                    else
                    {
                        Seek(Position + n, SeekOrigin.Begin);
                    }

                    break;
            }
        }

        public void SkipInt64() => Advance(sizeof(Int64));
        public void SkipUInt64() => Advance(sizeof(UInt64));
        public void SkipInt32() => Advance(sizeof(Int32));
        public void SkipUInt32() => Advance(sizeof(UInt32));
        public void SkipInt16() => Advance(sizeof(Int16));
        public void SkipUInt16() => Advance(sizeof(UInt16));
        public void SkipSByte() => Advance(sizeof(SByte));
        public void SkipByte() => Advance(sizeof(Byte));


        private void Advance(int n)
        {
            _remaining = _remaining.Slice(n);
        }
    }
}
