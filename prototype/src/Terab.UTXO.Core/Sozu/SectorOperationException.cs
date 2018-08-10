using System;

namespace Terab.UTXO.Core.Sozu
{
    /// <summary>
    /// Base class for all exceptions related to sector operations.
    /// <see cref="Sector"/>
    /// </summary>
    /// <remarks>
    /// Catch this base class exception if one doesn't want to catch every
    /// inherited specific sector exceptions.
    /// </remarks>

    public abstract class SectorOperationException : Exception
    {
        private protected SectorOperationException():base()
        {

        }

        private protected SectorOperationException(string message) : base(message)
        {

        }

        private protected SectorOperationException(string message, Exception inner) : base(message, inner)
        {

        }
    }
}
