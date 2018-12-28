// Copyright Lokad 2018 under MIT BCH.
using System;
using System.Runtime.InteropServices;
using LightningDB;

namespace Terab.Lib
{
    public class LightningStore<T> : IDisposable, IKeyValueStore<T> where T : unmanaged
    {
        public static T RoundTripKey = default(T);

        private const int PoolSizeInBytes = 65536;

        private LightningEnvironment _env;

        private string _dbName;

        private SpanPool<byte> _pool;

        public LightningStore(string pathToFolder, string dbName)
        {
            _env = new LightningEnvironment(pathToFolder);
            _dbName = dbName;
            _env.MaxDatabases = 1;
            _env.Open();
            _pool = new SpanPool<byte>(PoolSizeInBytes);
        }

        public void RoundTrip()
        {
            var span = MemoryMarshal.AsBytes(MemoryMarshal.CreateSpan(ref RoundTripKey, 1));
            var key = span.ToArray();

            using (var tx = _env.BeginTransaction())
            {
                using (var db = tx.OpenDatabase(_dbName,
                    new DatabaseConfiguration { Flags = DatabaseOpenFlags.Create }))
                {
                    tx.Put(db, key, key);
                    tx.Commit();
                }
            }

            using (var tx = _env.BeginTransaction())
            {
                using (var db = tx.OpenDatabase(_dbName,
                    new DatabaseConfiguration { Flags = DatabaseOpenFlags.Create }))
                {
                    tx.Delete(db, key);
                    tx.Commit();
                }
            }
        }


        public void Set(T key, ReadOnlySpan<byte> value)
        {
            var span = MemoryMarshal.AsBytes(MemoryMarshal.CreateSpan(ref key, 1));

            using (var tx = _env.BeginTransaction())
            {
                using (var db = tx.OpenDatabase(_dbName))
                {
                    tx.Put(db, span.ToArray(), value.ToArray());
                    tx.Commit();
                }
            }
        }

        /// <remarks>
        /// Returned <paramref name="value"/> is valid till next call to
        /// TryGet.
        /// </remarks>
        public bool TryGet(T key, out Span<byte> value)
        {
            _pool.Reset(); // clear previous Get result and free some space.
            var keySpan = MemoryMarshal.AsBytes(MemoryMarshal.CreateSpan(ref key, 1));
            using (var tx = _env.BeginTransaction(TransactionBeginFlags.ReadOnly))
            using (var db = tx.OpenDatabase(_dbName))
            {
                bool result = tx.TryGet(db, keySpan, out var valueSpan);
                if (result)
                {
                    value = GetSpan(valueSpan.Length);
                    valueSpan.CopyTo(value);
                }
                else
                {
                    value = Span<byte>.Empty;
                }
                
                return result;
            }
        }

        /// <summary>
        /// Check capacity of SpanPool and allocate a new one if capacity is
        /// not sufficient.
        /// </summary>
        private Span<byte> GetSpan(int length)
        {
            if (_pool.Capacity < length)
            {
                _pool = new SpanPool<byte>(Math.Max(length * 2, _pool.Capacity * 2));
            }
            return _pool.GetSpan(length);
        }

        public void Dispose()
        {
            _env.Dispose();
        }
    }
}