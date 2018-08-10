using System;
using Terab.UTXO.Core.Sozu;

namespace Terab.UTXO.Core.SectorStore
{
    /// <summary>
    /// Thrown after a stream read operation when the
    /// actual length of read data is less than what is
    /// expected.
    /// </summary>
    public class BufferPartiallyFilledException : SectorOperationException
    {
        public BufferPartiallyFilledException() : base()
        {
        }

        public BufferPartiallyFilledException(string message) : base(message)
        {
        }

        public BufferPartiallyFilledException(string message, Exception inner) : base(message, inner)
        {
        }
    }
}