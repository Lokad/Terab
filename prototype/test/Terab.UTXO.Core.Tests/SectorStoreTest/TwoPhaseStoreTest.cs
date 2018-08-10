using System;
using System.IO;
using Terab.UTXO.Core.SectorStore;
using Terab.UTXO.Core.Sozu;
using Xunit;

namespace Terab.UTXO.Core.Tests.SectorStoreTest
{
    public class TwoPhaseStoreFixture : ISectorStoreFixture
    {
        public int SectorSize { get; }

        public Random Random { get; }

        public string TestStorePath { get; }

        public string TestHotPath { get; }

        public TwoPhaseStoreFixture()
        {
            SectorSize = 200;
            Random = new Random(42);
            TestStorePath = @"testStore";
            TestHotPath = @"hot-testStore";
        }

        public ISectorStore Create()
        {
            return new TwoPhaseStore(SectorSize, TestStorePath);
        }

        public void Close(ISectorStore store)
        {
            store.Dispose();
            File.Delete(TestStorePath);
            File.Delete(TestHotPath);
        }

        public bool CheckFileExistence()
        {
            return File.Exists(TestStorePath) & File.Exists(TestHotPath);
        }
    }

    public class TwoPhaseStoreTest : AbstractSectorStoreTest<TwoPhaseStoreFixture>
    {
        public TwoPhaseStoreTest(TwoPhaseStoreFixture fixture) : base(fixture)
        {
        }

        /// <summary>
        /// Test EnsurePersistence, then init from persisted file
        /// </summary>
        [Fact]
        public void TryRecoveryTest()
        {
            const int numberOfSectorTesting = 5;
            _store.Initialize();
            // manually make a sector
            var sector = new Sector(_sectorSize);

            var contentBytes = new byte[sector.Contents.Length];
            for (int i = 0; i < numberOfSectorTesting; i++)
            {
                _rnd.NextBytes(contentBytes);
                contentBytes.AsSpan().CopyTo(sector.Contents);

                var index = _store.AllocateSectors(4);
                _store.Write(index, sector);
            }

            _store.EnsurePersistence();
            _store.Dispose();

            // corrupt main store file externally
            var corruption = new FileStream(_storeFixiture.TestStorePath, FileMode.Open, FileAccess.ReadWrite,
                FileShare.None);
            corruption.Seek(-1, SeekOrigin.End);
            corruption.WriteByte(0x00);
            corruption.Close();

            // reinit store
            _store = _storeFixiture.Create();
            _store.Initialize();
            var storeProvided = _store.Read(new SectorIndex((numberOfSectorTesting - 1) * 4));

            Assert.True(storeProvided.Contents.SequenceEqual(contentBytes.AsSpan()));
        }
    }
}