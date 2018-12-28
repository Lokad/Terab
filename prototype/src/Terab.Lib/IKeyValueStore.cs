// Copyright Lokad 2018 under MIT BCH.
using System;

namespace Terab.Lib
{
    /// <summary> Abstracts a generic low-level key value store. </summary>
    public interface IKeyValueStore<in T> where T : unmanaged
    {
        bool TryGet(T key, out Span<byte> value);

        void Set(T key, ReadOnlySpan<byte> value);

        /// <summary>
        /// Ensure store's accessibility, by insert then delete a test key.
        /// </summary>
        void RoundTrip();
    }
}
