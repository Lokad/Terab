using System;
using System.IO;
using Terab.UTXO.Core.SectorStore;

namespace Terab.UTXO.Core.Tests.SectorStoreTest
{
    public class VolatileStoreFixture:ISectorStoreFixture
    {
        public int SectorSize { get; }

        public Random Random { get; }

        public string TestStorePath { get; }

        public VolatileStoreFixture()
        {
            SectorSize = 200;
            Random = new Random(42);
            TestStorePath = ".\\volTestStore";
        }

        public ISectorStore Create()
        {
            return new VolatileStore(SectorSize, TestStorePath);
        }

        public void Close(ISectorStore store)
        {
            store.Dispose();
            File.Delete(TestStorePath);
        }

        public bool CheckFileExistence()
        {
            return File.Exists(TestStorePath);
        }

    }

    public class VolatileStoreTest : AbstractSectorStoreTest<VolatileStoreFixture>
    {
        public VolatileStoreTest(VolatileStoreFixture fixture): base(fixture)
        {

        }
      
    }
}
