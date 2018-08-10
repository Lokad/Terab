using System.Collections.Generic;
using System.Linq;

namespace Terab.UTXO.Core.Blockchain
{
    /// <summary>
    /// Based on the <see cref="SimpleBlockchain"/>, represents an optimized
    /// version of the blockchain specialised for the operations
    /// <see cref="IsAncestor"/> and <see cref="IsPruneable"/>.
    /// </summary>
    public class OptimizedLineage : ILineage
    {
        /// <summary>
        /// Only blocks not on the main chain are stored, with their parents, so
        /// that ancestry can always be resolved.
        /// </summary>
        private readonly IReadOnlyDictionary<BlockAlias, BlockAlias> _quasiOrphans; // dict orphan parent

        /// <see cref="IsPruneable"/>
        private readonly BlockAlias _pruneLimit;

        /// <summary>
        /// Create an optimized lineage that knows the quasi orphans and the
        /// prune limit, so that the query functions <see cref="IsAncestor"/>
        /// and <see cref="IsPruneable"/> can easily be executed.
        /// </summary>
        public OptimizedLineage(IReadOnlyDictionary<BlockAlias, BlockAlias> quasiOrphans, BlockAlias pruneLimit)
        {
            // TODO: [vermorel] check arguments

            _quasiOrphans = quasiOrphans;
            _pruneLimit = pruneLimit;
        }

        /// <summary>
        /// If a block is recent, it can be of interest to know whether
        /// another block is its ancestor.
        /// </summary>
        public bool IsAncestor(BlockAlias tip, BlockAlias maybeAncestor)
        {
            // We climb down the side chain until we find the parent,
            // a block that is lower than the maybeAncestor or we reach the main chain
            var parent = tip;
            do
            {
                // Move up the side chain looking for an ancestor on the main branch
                // A block counts as its own ancestor
                if (parent == maybeAncestor)
                    return true;
                // The ancestor has to have a smaller number than anything that comes afterward.
                if (parent.IsPrior(maybeAncestor))
                    return false;
            } while (_quasiOrphans.TryGetValue(parent, out parent));

            /*
             *                 o
             *                 |
             *                 o
             *                 |
             *                 o----o <- maybeAncestorAlias
             *                 |
             *                 o <- parent
             *                / \
             *  mainChain -> o   o <- blockAlias
             */

            // Here, 'block' is the most recent ancestor of the original block
            // on the main chain, and 'maybeAncestor < block', so the ancestor
            // will be an ancestor if and only if it is also on the main chain.

            return !_quasiOrphans.ContainsKey(maybeAncestor);
        }

        /// <summary>
        /// Returns whether a block is considered pruneable (i.e. it can
        /// be removed from the chain). The necessary criteria for this decision
        /// are that it is not on the main chain and that its first ancestor on
        /// the main chain is below or equal to the <see cref="_pruneLimit"/>.
        /// </summary>
        public bool IsPruneable(BlockAlias block)
        {
            // block is on the main chain
            if (!_quasiOrphans.ContainsKey(block))
                return false;

            // TODO: [vermorel] I suggest to usethe code below instead (more readable)
            // to be double checked
            //if (_quasiOrphans.TryGetValue(block, out var parent))
            //{
            //    while (_quasiOrphans.Keys.Contains(parent))
            //    {
            //        _quasiOrphans.TryGetValue(parent, out parent);
            //    }
            //}

            // Find first ancestor on the main chain and test if it is below the limit
            _quasiOrphans.TryGetValue(block, out var parent);
            while (_quasiOrphans.Keys.Contains(parent))
            {
                _quasiOrphans.TryGetValue(parent, out parent);
            }

            return parent.IsPrior(_pruneLimit) || parent == _pruneLimit;
        }
    }

}
