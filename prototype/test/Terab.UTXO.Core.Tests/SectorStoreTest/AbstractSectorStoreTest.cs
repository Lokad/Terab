using System;
using Terab.UTXO.Core.Sozu;
using System.Text;
using Terab.UTXO.Core.SectorStore;
using Xunit;

namespace Terab.UTXO.Core.Tests.SectorStoreTest
{
    /// <summary>
    /// Base class for testing common behaviors of ISectorStore
    /// Derived class could include specific test of derived ISectorStore 
    /// </summary>
    /// <typeparam name="TFixture"></typeparam>
    public abstract class AbstractSectorStoreTest<TFixture> : IDisposable, IClassFixture<TFixture> 
                                        where TFixture : class, ISectorStoreFixture
    {
        protected readonly int _sectorSize;

        protected readonly Random _rnd;

        protected ISectorStore _store;

        protected TFixture _storeFixiture;


        protected AbstractSectorStoreTest(TFixture fixture)
        {
            _sectorSize = fixture.SectorSize;
            _rnd = fixture.Random;
            _store = fixture.Create();
            _storeFixiture = fixture;
        }

        /// <summary>
        /// Test init method.
        /// </summary>
        [Fact]
        public void StoreInit()
        {
            _store.Initialize();
            Assert.True(_storeFixiture.CheckFileExistence(), "Cannot find hot store persistence file or store persistence file! ");
        }

        /// <summary>
        /// Test single write then read operation.
        /// </summary>
        [Fact]
        public void StoreWriteRead()
        {
            SectorIndex index = new SectorIndex(3);
            _store.Initialize();
            // manually make a sector
            var sector = new Sector(_sectorSize);

            var rnd = new Random(42);
            var contentBytes = new byte[sector.Contents.Length];
            rnd.NextBytes(contentBytes);
            contentBytes.AsSpan().CopyTo(sector.Contents);

            _store.AllocateSectors(4);
            _store.Write(index, sector);

            var storeProvided = _store.Read(index);
            Assert.True(storeProvided.Contents.SequenceEqual(contentBytes.AsSpan()));
        }

        /// <summary>
        /// Test double write then read operation.
        /// </summary>
        [Fact]
        public void StoreAlternateWriteRead()
        {
            SectorIndex index = new SectorIndex(1);
            _store.Initialize();
            // manually make a sector
            var sector = new Sector(_sectorSize);

            var contentBytes = new byte[sector.Contents.Length];
            _rnd.NextBytes(contentBytes);
            contentBytes.AsSpan().CopyTo(sector.Contents);

            _store.AllocateSectors(4);
            _store.Write(index, sector);

            var storeProvided = _store.Read(index);

            // second write
            var contentBytes2 = new byte[sector.Contents.Length];
            _rnd.NextBytes(contentBytes2);
            contentBytes2.AsSpan().CopyTo(sector.Contents);

            SectorIndex otherIndex = new SectorIndex(2);
            _store.Write(otherIndex, sector);

            // read first sector
            storeProvided = _store.Read(index);

            Assert.True(storeProvided.Contents.SequenceEqual(contentBytes.AsSpan()));
        }

        /// <summary>
        /// Test EnsurePersistence, then init from persisted file
        /// </summary>
        [Fact]
        public void EnsurePersistenceTest()
        {
            var numberOfSectorTesting = 5;
            _store.Initialize();
            // manually make a sector
            var sector = new Sector(_sectorSize);

            var contentBytes = new byte[sector.Contents.Length];
            for (var i = 0; i < numberOfSectorTesting; i++)
            {
                _rnd.NextBytes(contentBytes);
                contentBytes.AsSpan().CopyTo(sector.Contents);

                var index = _store.AllocateSectors(4);
                _store.Write(index, sector);
            }

            _store.EnsurePersistence();
            _store.Dispose();

            // reinit store
            _store = _storeFixiture.Create();

            _store.Initialize();
            var sectorIndex = new SectorIndex((numberOfSectorTesting - 1) * 4);
            var storeProvided = _store.Read(sectorIndex);

            Assert.True(storeProvided.Contents.SequenceEqual(contentBytes.AsSpan()));
        }


        public void Dispose()
        {
            _storeFixiture.Close(_store);
        }
    }
}
