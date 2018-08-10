using System;
using Terab.UTXO.Core.Sozu;

namespace Terab.UTXO.Core.SectorStore
{
    /// <summary>
    /// When data read from a single sector don't match what
    /// caller has expected, this exception is thrown.
    /// </summary>
    public class SectorInvalidDataException : SectorOperationException
    {
        public SectorInvalidDataException(string message) : base(message)
        {
        }

        public SectorInvalidDataException(string message, Exception inner) : base(message, inner)
        {
        }
    }
}