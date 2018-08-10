using System.Collections.Generic;

namespace Terab.UTXO.Core.Blockchain
{
    /// <summary>
    /// Provides algorithms performed on a <see cref="SimpleBlockchain"/>
    /// that go beyond basic manipulations of the data structure.
    /// </summary>
    public class BlockchainStrategies
    {
        /// <summary>
        /// Based on the raw representation of the blockchain,
        /// it filters out all blocks that are not on the main chain
        /// and returns them in a dictionary that links a quasi orphaned
        /// block to its parent.
        /// </summary>
        public Dictionary<BlockAlias, BlockAlias> GetQuasiOrphans(SimpleBlockchain chain)
        {
            var maxHeightElements = new List<BlockAlias>();
            var height = chain.BlockchainHeight;

            // Recover all elements that have maximal block height.
            // If there is more than one block with maximal block height, there
            // are branches at the end of the blockchain all apart from one will have to
            // be considered quasiOrphans.
            foreach (var block in chain.GetCommitted())
            {
                if (block.BlockHeight == height)
                {
                    maxHeightElements.Add(block.Alias);
                }
            }

            foreach (var block in chain.GetUncommitted())
            {
                if (block.BlockHeight == height)
                {
                    maxHeightElements.Add(block.Alias);
                }
            }

            // Go to the oldest element with the maximal blockheight which is by definition
            // the start of the main chain.
            foreach (var block in chain.GetReverseEnumerator())
            {
                if (maxHeightElements.Count == 1)
                    break;
                if (block.BlockHeight == height)
                    maxHeightElements.Remove(block.Alias);
            }

            // Get all quasiOrphans by collecting everything apart from
            // the parent of the last block seen on the main chain.
            var quasiOrphans = new Dictionary<BlockAlias, BlockAlias>();
            var nextMainChainBlock = maxHeightElements[0];

            foreach (var block in chain.GetReverseEnumerator())
            {
                if (block.Alias == nextMainChainBlock)
                {
                    // we are on the main chain and need to update the next parent
                    nextMainChainBlock = block.Parent;
                }
                else
                {
                    // quasiOrphan
                    quasiOrphans.Add(block.Alias, block.Parent);
                }
            }

            return quasiOrphans;
        }
    }
}