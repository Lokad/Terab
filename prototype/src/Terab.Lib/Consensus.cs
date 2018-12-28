// Copyright Lokad 2018 under MIT BCH.
namespace Terab.Lib
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

        /// <summary>
        /// Hash of the first block of the Bitcoin blockchain, as documented at
        /// https://en.bitcoin.it/wiki/Genesis_block
        /// </summary>
        public const string GenesisBlockIdHex =
            "000000000019d6689c085ae165831e934ff763ae46a2a6c172b3f1b60a8ce26f";
    }
}
