using Terab.UTXO.Core.Sozu;

namespace Terab.UTXO.Core.SectorStore
{
    /// <summary>
    /// Thrown when facing a non-recoverable storage corruption.
    /// </summary>
    public class CorruptedStorageException : SectorOperationException
    {
    }
}
