namespace Terab.UTXO.Core.Blockchain
{
    /// <summary>
    /// Supports graph operations on a quasi-linear chain of blocks.
    /// </summary>
    /// <remarks>
    /// The balance associated with an address as defined within a block remains
    /// the same until the balance is re-defined in a descendent block. The
    /// update should only be taken into account if the two blocks of interest
    /// are part of the same lineage.
    /// </remarks>
    public interface ILineage
    {
        /// <summary>
        /// Returns 'true' if a TXO that has been marked as consumed in 'block'
        /// can now be pruned because the block is old enough.
        /// </summary>
        /// <remarks>
        /// In a full TXO configuration, this method always returns 'false'.
        /// </remarks>
        bool IsPruneable(BlockAlias block);

        /// <summary>
        /// Returns 'true' if 'maybeAncestor' is indeed an ancestor of 'block'.
        /// This method is intended to support checking whether the balance
        /// associated to an address can be considered as the most recent
        /// valid balance entry for this address.
        /// </summary>
        bool IsAncestor(BlockAlias tip, BlockAlias maybeAncestor);
    }
}
