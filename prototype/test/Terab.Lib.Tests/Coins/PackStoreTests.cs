// Copyright Lokad 2018 under MIT BCH.
using System;
using System.IO;
using Terab.Lib.Coins;
using Terab.Lib.Tests.Mock;
using Xunit;

namespace Terab.Lib.Tests.Coins
{
    public class PackStoreFixture
    {
        private PackStore _store;

        private readonly int[] _sectorsSize = {4096, 4096 * 3, 4096};

        private string[] _storesPath;

        private string _journalPath;

        private IKeyValueStore<uint> _genericStore;

        public int[] SectorsSize => _sectorsSize;

        public PackStore Store => _store;

        public IKeyValueStore<uint> GenericKeyValueStore => _genericStore;

        public PackStoreFixture()
        {
            _storesPath = new string[_sectorsSize.Length];
            _genericStore = new VolatileKeyValueStore<uint>();
        }

        public void InitStorePaths(string tempFolder)
        {
            for (var i = 0; i < _storesPath.Length; ++i)
            {
                _storesPath[i] = Path.Join(tempFolder, Guid.NewGuid().ToString());
            }
            _journalPath = Path.Join(tempFolder, Guid.NewGuid().ToString());
        }

        public void CreateStore()
        {
            var layers = new MemoryMappedFileSlim[_storesPath.Length];
            for (var i = 0; i < _storesPath.Length; ++i)
            {
                layers[i] = new MemoryMappedFileSlim(_storesPath[i], _sectorsSize[i] * 64);
            }
           
            var journal = new MemoryMappedFileSlim(_journalPath, Constants.CoinStoreJournalCapacity);
            _store = new PackStore(64, _sectorsSize, layers, journal, _genericStore);
            _store.Initialize();
        }


        public void CloseStore()
        {
            _store?.Dispose();
        }

        public FileStream GetFileStream(int layer)
        {
            return new FileStream(_storesPath[layer], FileMode.Open, FileAccess.ReadWrite);
        }
    }

    [Collection("SharedFileSystem")]
    public class MemmapPackStoreTests : IClassFixture<PackStoreFixture>
    {
        private PackStoreFixture _fixture;


        public MemmapPackStoreTests(PackStoreFixture fixture, SharedFileSystem fileSystem)
        {
            _fixture = fixture;
            _fixture.InitStorePaths(fileSystem.CreateNewDirectoryForTest());
            _fixture.CreateStore();
            _fixture.CloseStore();
        }

        [Fact]
        public void ReadTest()
        {
            using (var fs = _fixture.GetFileStream(0))
            {
                int sectorSize = _fixture.SectorsSize[0];
                fs.Seek(_fixture.SectorsSize[0] * 2, SeekOrigin.Begin);
                Span<byte> span = stackalloc byte[sectorSize];
                span[0] = 2;
                fs.Close();
            }

            try
            {
                _fixture.CreateStore();
                var pack = _fixture.Store.Read(0, 2);
                Assert.Equal(0, pack.LayerIndex);
                Assert.Equal((uint) 2, pack.SectorIndex);
            }
            finally
            {
                _fixture.CloseStore();
            }
        }


        private void WriteSector(uint sectorIndex, Span<byte> s0, Span<byte> s1, Span<byte> s2, Span<byte> s3)
        {
            var p0 = new CoinPack(s0)
            {
                LayerIndex = 0, SectorIndex = sectorIndex, Version = CoinPack.CurrentVersion,
                SizeInBytes = _fixture.SectorsSize[0]
            };
            var p1 = new CoinPack(s1)
            {
                LayerIndex = 1, SectorIndex = sectorIndex, Version = CoinPack.CurrentVersion,
                SizeInBytes = _fixture.SectorsSize[1]
            };
            var p2 = new CoinPack(s2)
            {
                LayerIndex = 2, SectorIndex = sectorIndex, Version = CoinPack.CurrentVersion,
                SizeInBytes = _fixture.SectorsSize[2]
            };

            var collection = new ShortCoinPackCollection(p0);
            collection.Add(p1);
            collection.Add(p2);

            if (!s3.IsEmpty)
            {
                var p3 = new CoinPack(s3)
                {
                    LayerIndex = 3, SectorIndex = sectorIndex, Version = CoinPack.CurrentVersion,
                    SizeInBytes = s3.Length
                };
                collection.Add(p3);
            }

            try
            {
                _fixture.CreateStore();
                _fixture.Store.Write(collection);
            }
            finally
            {
                _fixture.CloseStore();
            }
        }

        [Fact]
        public void WriteTest()
        {
            uint sectorIndex = 12;
            var rand = new Random(4);

            Span<byte> s0 = stackalloc byte[_fixture.SectorsSize[0]];
            Span<byte> s1 = stackalloc byte[_fixture.SectorsSize[1]];
            Span<byte> s2 = stackalloc byte[_fixture.SectorsSize[2]];
            Span<byte> s3 = stackalloc byte[4096];
            rand.NextBytes(s0);
            rand.NextBytes(s1);
            rand.NextBytes(s2);
            rand.NextBytes(s3);

            WriteSector(sectorIndex, s0, s1, s2, s3);

            using (var fs = _fixture.GetFileStream(0))
            {
                fs.Seek(_fixture.SectorsSize[0] * sectorIndex, SeekOrigin.Begin);
                Span<byte> span = stackalloc byte[_fixture.SectorsSize[0]];
                fs.Read(span);
                Assert.True(span.SequenceEqual(s0));
                fs.Close();
            }

            using (var fs = _fixture.GetFileStream(1))
            {
                Span<byte> span = stackalloc byte[_fixture.SectorsSize[1]];
                fs.Seek(_fixture.SectorsSize[1] * sectorIndex, SeekOrigin.Begin);
                fs.Read(span);
                Assert.True(span.SequenceEqual(s1));
                fs.Close();
            }

            using (var fs = _fixture.GetFileStream(2))
            {
                Span<byte> span = stackalloc byte[_fixture.SectorsSize[2]];
                fs.Seek(_fixture.SectorsSize[2] * sectorIndex, SeekOrigin.Begin);
                fs.Read(span);
                Assert.True(span.SequenceEqual(s2));
                fs.Close();
            }

            _fixture.GenericKeyValueStore.TryGet(sectorIndex, out var s3_bis);
            Assert.True(s3_bis.SequenceEqual(s3));
        }

        [Fact]
        public void TestJournalIntegrity()
        {
            uint sectorIndex = 15;
            var rand = new Random(4);

            Span<byte> s0 = stackalloc byte[_fixture.SectorsSize[0]];
            Span<byte> s1 = stackalloc byte[_fixture.SectorsSize[1]];
            Span<byte> s2 = stackalloc byte[_fixture.SectorsSize[2]];
            Span<byte> s3 = stackalloc byte[4096];
            rand.NextBytes(s0);
            rand.NextBytes(s1);
            rand.NextBytes(s2);
            rand.NextBytes(s3);

            WriteSector(sectorIndex, s0, s1, s2, s3);

            try
            {
                _fixture.CreateStore();
                Assert.True(_fixture.Store.CheckJournalIntegrity());
                Assert.True(_fixture.Store.CompareWithJournal());
            }
            finally
            {
                _fixture.CloseStore();
            }
        }


        [Fact]
        public void TestRecoveryFromJournal()
        {
            uint sectorIndex = 17;
            var rand = new Random(4);

            Span<byte> s0 = stackalloc byte[_fixture.SectorsSize[0]];
            Span<byte> s1 = stackalloc byte[_fixture.SectorsSize[1]];
            Span<byte> s2 = stackalloc byte[_fixture.SectorsSize[2]];
            Span<byte> s3 = stackalloc byte[4096];
            rand.NextBytes(s0);
            rand.NextBytes(s1);
            rand.NextBytes(s2);
            rand.NextBytes(s3);

            WriteSector(sectorIndex, s0, s1, s2, s3);

            // corrupt a layered storage
            using (var fs = _fixture.GetFileStream(0))
            {
                fs.Seek(_fixture.SectorsSize[0] * sectorIndex, SeekOrigin.Begin);
                Span<byte> temp = stackalloc byte[_fixture.SectorsSize[0]];
                fs.Write(temp);
                fs.Close();
            }

            try
            {
                _fixture.CreateStore();
                Assert.False(_fixture.Store.CompareWithJournal());
                _fixture.Store.RecoverFromJournal();
                Assert.True(_fixture.Store.CompareWithJournal());
            }
            finally
            {
                _fixture.CloseStore();
            }
        }
    }
}