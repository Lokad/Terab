using System;
using Terab.UTXO.Core.Blockchain;
using Terab.UTXO.Core.Hash;
using Terab.UTXO.Core.SectorStore;

namespace Terab.UTXO.Core.Sozu
{
    public class SozuFactory
    {
        public const int TwoPhaseStoreType = 0;
        public const int VolatileStoreType = 1;

        private readonly ILineage _lineage;

        private readonly Hash256 _start;
        private readonly Hash256 _end;
        private readonly string _path;
        private readonly SozuFactoryConfig[] _sozuFactoryConfig;

        public SozuFactory(ILineage lineage, 
            Hash256 start, Hash256 end, SozuFactoryConfig[] sozuFactoryConfig,
            string path)
        {
            _lineage = lineage;
            _start = start;
            _end = end;
            _sozuFactoryConfig = sozuFactoryConfig;
            _path = path;
        }

        private static ISectorStore CreateSectorStore(int typeOfStore, string path, int sectorSizeInBytes)
        {
            switch (typeOfStore)
            {
                case TwoPhaseStoreType:
                    return new TwoPhaseStore(sectorSizeInBytes, path);
                case VolatileStoreType:
                    return new VolatileStore(sectorSizeInBytes, path);
                default:
                    throw new ArgumentException("Unknown store type", nameof(typeOfStore));
            }
        }

        public SozuTable Create()
        {
            SozuTable subTable = null;
            for (var i = _sozuFactoryConfig.Length - 1; i >= 0; --i)
            {
                var config = _sozuFactoryConfig[i];
                var newTable = new SozuTable(
                    new SozuConfig(i == 0, config.SectorBitDepth, _start, _end),
                    _lineage,
                    CreateSectorStore(config.TypeOfStore, string.Format(_path, i), config.SectorSizeInBytes),
                    subTable);
                subTable = newTable;
            }

            return subTable;
        }
    }
}