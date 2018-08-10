using Terab.UTXO.Core.Hash;

namespace Terab.UTXO.Core.Sozu
{
    // TODO: [vermorel] Scope and intent of 'SozuConfig' should be clarified.

    public class SozuConfig
    {
        /// <summary>Only the first layer has implicit sectors.</summary>
        public bool ImplicitSectors { get; }

        /// <summary>
        /// How many bits do all outpoints share within each sector.
        /// </summary>
        public int SectorBitDepth { get; }

        public Hash256 Start { get; }

        public Hash256 End { get; }

        public SozuConfig(bool implicitSectors, int sectorBitDepth, Hash256 start, Hash256 end)
        {
            ImplicitSectors = implicitSectors;
            SectorBitDepth = sectorBitDepth;
            Start = start;
            End = end;
        }
    }
}
