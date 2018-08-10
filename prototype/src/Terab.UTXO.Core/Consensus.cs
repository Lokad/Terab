namespace Terab.UTXO.Core
{
    /// <summary>
    /// Constants originating from the Bitcoin protocol.
    /// </summary>
    public class Consensus
    {
        /// <summary>
        /// Max size in bytes of Outpoint script, defined in script.h
        /// <see cref="https://github.com/Bitcoin-ABC/bitcoin-abc"/>
        /// </summary>
        public const int MaxScriptSizeInBytes = 10_000;
    }
}
