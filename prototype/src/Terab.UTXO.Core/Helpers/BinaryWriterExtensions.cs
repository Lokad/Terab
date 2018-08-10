using System.IO;
using Terab.UTXO.Core.Hash;

namespace Terab.UTXO.Core.Helpers
{
    /// <summary>
    /// Makes the writing of proprietary types into a BinaryWriter more readable.
    /// </summary>
    public static class BinaryWriterExtensions
    {
        /// <summary>
        /// Facilitates the writing of <see cref="Terab.UTXO.Core.Hash.Hash256"/>.
        /// </summary>
        public static void WriteHash256(this BinaryWriter writer, Hash256 hash)
        {
            writer.Write(hash.Var1);
            writer.Write(hash.Var2);
            writer.Write(hash.Var3);
            writer.Write(hash.Var4);
        }

        public static void WriteHash128(this BinaryWriter writer, Hash128 hash)
        {
            writer.Write(hash.Left);
            writer.Write(hash.Right);
        }
    }
}
