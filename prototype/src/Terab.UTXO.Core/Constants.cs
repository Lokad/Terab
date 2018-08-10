namespace Terab.UTXO.Core
{
    public class Constants
    {
        /// <summary>
        /// Max number of outpoints that could be contained in a standard
        /// TxoPack.
        /// </summary>
        public const int MaxOutpointsCount = 30;

        /// <summary>
        /// Max length of standard TxoPack's all outpoints payloads
        /// </summary>
        // <remarks>
        // A payload is a bit larger than its inside script
        // </remarks>
        public const int MaxPayloadsCapacity = 320_000;

        /// <summary>
        /// Max value of Terab BlockAlias
        /// </summary>
        /// <remarks>
        /// We use only 3 bytes among 4 bytes of uint to save a blockalias
        /// It let us to encode a blockevent, basically blockalias with
        /// sign, still using 4 bytes. (Higher bits encode Consumption flag)
        /// </remarks>
        public const uint MaxBlockAliasValue = 1 << 24 - 1;

        /// <summary>
        /// If an outpoint script legnth exceeds this limit, it's considered as
        /// too large. It will activate <see cref="OutpointFlags.IsLargeScript"/>
        /// flag.
        /// </summary>
        public const int LargeScriptLength = 256;
    }
}