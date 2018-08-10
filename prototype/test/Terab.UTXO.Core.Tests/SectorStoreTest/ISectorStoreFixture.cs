using System;
using Terab.UTXO.Core.SectorStore;

namespace Terab.UTXO.Core.Tests.SectorStoreTest
{
    /// <summary>
    /// Common interface for SectoreStore test fixtures
    /// </summary>
    public interface ISectorStoreFixture
    {
        ISectorStore Create();

        void Close(ISectorStore store);

        bool CheckFileExistence();

        int SectorSize { get; }

        Random Random { get; }
    }
}
