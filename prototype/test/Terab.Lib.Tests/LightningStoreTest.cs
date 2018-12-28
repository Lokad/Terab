// Copyright Lokad 2018 under MIT BCH.
using System;
using System.IO;
using System.Text;
using Xunit;

namespace Terab.Lib.Tests
{
    public class SharedFileSystem : IDisposable
    {
        private readonly string _testTempDir;

        public SharedFileSystem()
        {
            _testTempDir = Path.Combine(Directory.GetCurrentDirectory(), "testrun");
        }

        public string CreateNewDirectoryForTest()
        {
            var path = Path.Combine(_testTempDir, Guid.NewGuid().ToString());
            Directory.CreateDirectory(path);
            return path;
        }

        public void Dispose()
        {
            Directory.Delete(_testTempDir, true);
        }
    }

    [CollectionDefinition("SharedFileSystem")]
    public class SharedFileSystemCollection : ICollectionFixture<SharedFileSystem>
    {
    }

    [Collection("SharedFileSystem")]
    public class LightningStoreTest : IDisposable
    {
        private LightningStore<uint> _store;


        public LightningStoreTest(SharedFileSystem fileSystem)
        {
            var path = fileSystem.CreateNewDirectoryForTest();
            _store = new LightningStore<uint>(path, "Test");
        }

        [Fact]
        public void RoundTripTest()
        {
            _store.RoundTrip();
            var result = _store.TryGet(LightningStore<uint>.RoundTripKey, out var value);
            Assert.False(result);
        }

        [Fact]
        public void SetThenGet()
        {
            _store.RoundTrip();
            _store.Set(10, Encoding.UTF8.GetBytes("world"));
            _store.Set(12, Encoding.UTF8.GetBytes("hello"));
            bool result = _store.TryGet(10, out var value);
            Assert.True(result);
            Assert.Equal("world", Encoding.UTF8.GetString(value.ToArray()));
            result = _store.TryGet(11, out value);
            Assert.False(result);
        }

        public void Dispose()
        {
            _store.Dispose();
        }
    }
}