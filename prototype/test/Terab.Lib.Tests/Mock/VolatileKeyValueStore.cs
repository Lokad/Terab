// Copyright Lokad 2018 under MIT BCH.
using System;
using System.Collections.Generic;

namespace Terab.Lib.Tests.Mock
{
    /// <summary>
    /// In memory mock of <see cref="IKeyValueStore{T}"/>
    /// </summary>
    public class VolatileKeyValueStore<T> : IKeyValueStore<T> where T : unmanaged
    {
        private const int PoolSizeInBytes = 65536;

        private Dictionary<T, byte[]> _map;

        private readonly SpanPool<byte> _pool;

        public VolatileKeyValueStore()
        {
            _map  = new Dictionary<T, byte[]>();
            _pool = new SpanPool<byte>(PoolSizeInBytes);
        }

        public bool TryGet(T key, out Span<byte> value)
        {
            _pool.Reset();
            if (_map.TryGetValue(key, out var vs))
            {
                value = _pool.GetSpan(vs.Length);
                vs.CopyTo(value);
                return true;
            }
            else
            {
                value = Span<byte>.Empty;
                return false;
            }
        }

        public void Set(T key, ReadOnlySpan<byte> value)
        {
            _map[key] = value.ToArray();
        }

        public void RoundTrip()
        {
            // as the store is in memory, this implementation is dummy.
        }
    }
}
