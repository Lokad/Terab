using System;
using System.IO;

namespace Terab.UTXO.Core.Helpers
{
    /// <summary>
    /// Implements the combined functionality of a BinaryWriter + BinaryReader
    /// over a MemoryStream; but this time  over a <see cref="Span{Byte}"/>
    /// instead of a <c>byte[]</c>.
    /// </summary>
    /// <remarks>
    /// Because this is both a reader and a writer, it's called an editor.
    /// There are additional methods, too, such as <see cref="ClearInt32"/>
    /// </remarks>
    public ref struct SpanBinaryEditor
    {
        private Span<byte> _remaining; // because of that mutable member, the struct
                                       // has been made a ref struct, to ensure it's always
                                       // on the stack
        private readonly Span<byte> _buffer;

        public SpanBinaryEditor(Span<byte> buffer)
        {
            _remaining = buffer;
            _buffer = buffer;
        }

        public void WriteInt64(Int64 value)
        {
            System.Buffers.Binary.BinaryPrimitives.WriteInt64LittleEndian(_remaining, value);
            _remaining = _remaining.Slice(sizeof(Int64));
        }

        public void WriteInt64BE(Int64 value)
        {
            System.Buffers.Binary.BinaryPrimitives.WriteInt64BigEndian(_remaining, value);
            _remaining = _remaining.Slice(sizeof(Int64));
        }

        public void WriteUInt64(UInt64 value)
        {
            System.Buffers.Binary.BinaryPrimitives.WriteUInt64LittleEndian(_remaining, value);
            _remaining = _remaining.Slice(sizeof(Int64));
        }

        public void WriteUInt64BE(UInt64 value)
        {
            System.Buffers.Binary.BinaryPrimitives.WriteUInt64BigEndian(_remaining, value);
            _remaining = _remaining.Slice(sizeof(Int64));
        }

        public void WriteInt32(Int32 value)
        {
            System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(_remaining, value);
            _remaining = _remaining.Slice(sizeof(Int32));
        }

        public void WriteUInt32(UInt32 value)
        {
            System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(_remaining, value);
            _remaining = _remaining.Slice(sizeof(Int32));
        }

        public void WriteInt16(Int16 value)
        {
            System.Buffers.Binary.BinaryPrimitives.WriteInt16LittleEndian(_remaining, value);
            _remaining = _remaining.Slice(sizeof(Int16));
        }

        public void WriteUInt16(UInt16 value)
        {
            System.Buffers.Binary.BinaryPrimitives.WriteUInt64LittleEndian(_remaining, value);
            _remaining = _remaining.Slice(sizeof(Int16));
        }

        public void WriteSbyte(SByte value)
        {
            _remaining[0] = unchecked((byte) value);
            _remaining = _remaining.Slice(1);
        }

        public void WriteByte(Byte value)
        {
            _remaining[0] = value;
            _remaining = _remaining.Slice(1);
        }

        public void WriteBoolean(Boolean value)
            => WriteByte(value ? (byte)1 : (byte)0);

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

        public void ClearInt64() => Clear(sizeof(Int64));
        public void ClearUInt64() => Clear(sizeof(UInt64));
        public void ClearInt32() => Clear(sizeof(Int32));
        public void ClearUInt32() => Clear(sizeof(UInt32));
        public void ClearInt16() => Clear(sizeof(Int16));
        public void ClearUInt16() => Clear(sizeof(UInt16));
        public void ClearSByte() => Clear(sizeof(SByte));
        public void ClearByte() => Clear(sizeof(Byte));

        private void Advance(int n)
        {
            _remaining = _remaining.Slice(n);
        }

        private void Clear(int n)
        {
            for (int i = 0; i < n; ++i)
            {
                _remaining[i] = 0;
            }

            Advance(n);
        }
    }
}
