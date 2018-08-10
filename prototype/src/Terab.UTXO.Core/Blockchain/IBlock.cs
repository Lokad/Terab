namespace Terab.UTXO.Core.Blockchain
{
    /// <summary>
    /// Identifies a block in the blockchain for Terab purposes, meaning
    /// only the metadata important for Terab is stored.
    /// </summary>
    public interface IBlock
    {
        /// <summary>
        /// Height of a block in the blockchain. Multiple blocks can be of the
        /// same height but that means that they are not in the same branch.
        /// </summary>
        int BlockHeight { get; }

        /// <summary>
        /// The alias of a block which identifies blocks uniquely within the
        /// Terab application. It is used to produce a block handle which
        /// is used for communication with the client.
        /// </summary>
        BlockAlias Alias { get; }

        /// <summary>
        /// Alias of block that was emitted just before the present one.
        /// </summary>
        BlockAlias Parent { get; }
        
        // TODO: [vermorel] IsEmpty appears to be unused, it should probably be removed

        /// <summary>
        /// Indicates whether this is an empty block.
        /// </summary>
        /// <remarks>
        /// The hash of the very first block is not entirely 0, which is why all
        /// these conditions together indicate an invalid block.
        /// </remarks>
        bool IsEmpty { get; }
    }
}
