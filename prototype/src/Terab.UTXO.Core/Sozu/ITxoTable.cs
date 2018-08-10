using Terab.UTXO.Core.Hash;

namespace Terab.UTXO.Core.Sozu
{
    /// <summary>
    /// Internal API to represent a fragment of the Bitcoin TXO database.
    /// </summary>
    /// <remarks>
    /// The database is intended to be partitioned against the outpoint space. 
    /// The 'Start' and 'End' properties provide the scope of the table.
    /// </remarks>
    public interface ITxoTable
    {
        /// <summary>Inclusive Txid start on outpoints' hash.</summary>
        Hash256 Start { get; }

        /// <summary>Exclusive Txid end on outpoints' hash.</summary>
        Hash256 End { get; }

        /// <summary>
        /// Find data for outpoints in the first arg TxoPack,
        /// found and populated outpoints are filled in second arg TxoPack.
        /// </summary>
        /// <remarks>
        /// By mutating the 'TxoPack' object itself, we avoid the need to 
        /// allocate newer objects in order to return the results.
        /// </remarks>
        void Read(TxoPack txoIn, TxoPack txoOut);

        void Write(TxoPack txos);

        void EnsurePersistence();
    }
}
