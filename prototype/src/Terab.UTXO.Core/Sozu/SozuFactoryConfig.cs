namespace Terab.UTXO.Core.Sozu
{
    public class SozuFactoryConfig
    {
        public SozuFactoryConfig(int sectorBitDepth, int typeOfStore, int sectorSizeInBytes)
        {
            SectorBitDepth = sectorBitDepth;
            TypeOfStore = typeOfStore;
            SectorSizeInBytes = sectorSizeInBytes;
        }

        public int SectorBitDepth { get; }

        public int TypeOfStore { get; }

        public int SectorSizeInBytes { get; }
    }
}