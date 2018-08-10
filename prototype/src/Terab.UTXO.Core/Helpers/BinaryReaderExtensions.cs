using System.IO;
using Terab.UTXO.Core.Hash;

namespace Terab.UTXO.Core.Helpers
{
    /// <summary>
    /// Makes the reading of proprietary types by a BinaryReader more readable.
    /// </summary>
    public static class BinaryReaderExtensions
    {
        /// <summary>
        /// Facilitates the reading of <see cref="Terab.UTXO.Core.Hash.Hash256"/>.
        /// </summary>
        public static Hash256 ReadHash256(this BinaryReader reader)
        {
            var blockId1 = reader.ReadUInt64();
            var blockId2 = reader.ReadUInt64();
            var blockId3 = reader.ReadUInt64();
            var blockId4 = reader.ReadUInt64();
            return new Hash256(blockId1, blockId2, blockId3, blockId4);
        }

        public static Hash128 ReadHash128(this BinaryReader reader)
        {
            var blockId1 = reader.ReadUInt64();
            var blockId2 = reader.ReadUInt64();
            return new Hash128(blockId1, blockId2);
        }
    }
}
