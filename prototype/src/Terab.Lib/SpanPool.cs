// Copyright Lokad 2018 under MIT BCH.
using System;

namespace Terab.Lib
{
    /// <summary>
    /// Helper intended to dynamically allocate 'span' of varying
    /// sizes while leveraging a common buffer.
    /// </summary>
    public class SpanPool<T> where T : unmanaged
    {
        private readonly T[] _buffer;

        private int _offset;

        public int Capacity => _buffer.Length;

        public int Offset => _offset;

        public SpanPool(int length)
        {
            _buffer = new T[length];
            _offset = 0;
        }

        /// <summary> Allocation for the internal buffer. </summary>
        public Span<T> GetSpan(int length)
        {
            var span = new Span<T>(_buffer).Slice(_offset, length);
            _offset += length;

            return span;
        }

        /// <summary> Returns the whole segment already allocated through 'GetSpan'.</summary>
        public Span<T> Allocated()
        {
            return new Span<T>(_buffer, 0, _offset);
        }

        public void Reset()
        {
            // ensure clean buffers
            Array.Clear(_buffer, 0, _offset);
            _offset = 0;
        }
    }
}
