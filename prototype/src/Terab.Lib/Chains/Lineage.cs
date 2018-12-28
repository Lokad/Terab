// Copyright Lokad 2018 under MIT BCH.
using System;
using System.Collections.Generic;
using System.Linq;
using Terab.Lib.Coins;

namespace Terab.Lib.Chains
{
    /// <summary>
    /// Quasi O(1) performance on all lineage queries.
    /// </summary>
    public class Lineage : ILineage
    {
        public int CoinPruneHeight { get; set; }

        /// <remarks>
        /// Public property for unit testing purposes.
        /// </remarks>
        internal BlockAlias ChainTip { get; }

        private readonly IReadOnlyDictionary<BlockAlias, BlockAlias> _quasiOrphans;

        private readonly HashSet<BlockAlias> _uncommitted;

        public Lineage(
            IReadOnlyList<CommittedBlock> committed,
            IReadOnlyList<UncommittedBlock> uncommitted,
            int coinPruneLimitDistance)
        {
            _quasiOrphans = GetQuasiOrphans(committed, uncommitted);
            ChainTip = GetCommittedBlockOfMaxHeight(committed);
            CoinPruneHeight = ChainTip.BlockHeight - coinPruneLimitDistance;
            _uncommitted = new HashSet<BlockAlias>(uncommitted.Select(x => x.Alias));
        }

        /// <summary>
        /// Based on the raw representation of the blockchain,
        /// it filters out all blocks that are not on the main chain
        /// and returns them in a dictionary that links a quasi orphaned
        /// block to its parent.
        /// </summary>
        private static Dictionary<BlockAlias, BlockAlias> GetQuasiOrphans(
            IReadOnlyList<CommittedBlock> committed,
            IReadOnlyList<UncommittedBlock> uncommitted)
        {
            var nextMainChainBlockAlias = GetBlockOfMaxHeight(committed, uncommitted);

            // Get all quasiOrphans by collecting everything apart from
            // the parent of the last block seen on the main chain.
            var quasiOrphans = new Dictionary<BlockAlias, BlockAlias>();

            foreach (var block in committed)
            {
                quasiOrphans.Add(block.Alias, block.Parent);
            }

            foreach (var block in uncommitted)
            {
                quasiOrphans.Add(block.Alias, block.Parent);
            }

            var currentAlias = nextMainChainBlockAlias;
            while (quasiOrphans.ContainsKey(currentAlias))
            {
                nextMainChainBlockAlias = quasiOrphans[currentAlias];
                quasiOrphans.Remove(currentAlias);
                currentAlias = nextMainChainBlockAlias;
            }

            return quasiOrphans;
        }

        /// <summary>
        /// Among all committed blocks, finds the one that is at the highest
        /// height. If there are multiple blocks at maximum height, the one that
        /// was first committed is returned.
        /// </summary>
        private static BlockAlias GetCommittedBlockOfMaxHeight(IReadOnlyList<CommittedBlock> committed)
        {
            if (committed.Count > 0)
            {
                var maxCommittedBlockHeight = committed.Max(b => b.BlockHeight);
                return committed.First(b => b.BlockHeight == maxCommittedBlockHeight).Alias;
            }

            return BlockAlias.Undefined;
        }

        private static BlockAlias GetBlockOfMaxHeight(
            IReadOnlyList<CommittedBlock> committed,
            IReadOnlyList<UncommittedBlock> uncommitted)
        {
            var maxBlock = BlockAlias.Undefined;
            var maxHeight = -1;

            foreach (var b in committed)
            {
                if (b.BlockHeight > maxHeight)
                {
                    maxHeight = b.BlockHeight;
                    maxBlock = b.Alias;
                }
            }

            foreach (var b in uncommitted)
            {
                if (b.BlockHeight > maxHeight)
                {
                    maxHeight = b.BlockHeight;
                    maxBlock = b.Alias;
                }
            }

            return maxBlock;
        }

        /// <summary>
        /// Returns true if <see cref="maybeAncestor"/> is an ancestor of
        /// <see cref="subject"/>, meaning they are on the same branch of the
        /// blockchain with <see cref="subject"/> being younger.
        /// </summary>
        private bool IsAncestor(BlockAlias subject, BlockAlias maybeAncestor)
        {
            if (subject.IsPrior(maybeAncestor))
                return false;

            // We climb down the side chain until we find the parent,
            // a block that is lower than the maybeAncestor or we reach the main chain
            var parent = subject;
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
             *  mainChain -> o   o <- subject
             */

            // Here, 'parent' is the most recent ancestor of the original block
            // ('subject') on the main chain, and 'maybeAncestor < parent', so the
            // ancestor will be an ancestor if and only if it is also on the
            // main chain.

            return !_quasiOrphans.ContainsKey(maybeAncestor);
        }

        /// <summary>
        /// Returns whether <see cref="block"/> is considered prunable (i.e. it can
        /// be removed from the chain). The necessary criteria for this decision
        /// are that it is not on the main chain and that its first ancestor on
        /// the main chain is below or equal to the prune limit.
        /// </summary>
        private bool IsBlockPermanentlyOrphaned(BlockAlias block)
        {
            var current = block;
            var isInMainChain = true;

            while (_quasiOrphans.TryGetValue(current, out var parent))
            {
                isInMainChain = false;
                current = parent;
            }

            return !isInMainChain && current.BlockHeight <= CoinPruneHeight;
        }

        public bool IsUncommitted(BlockAlias block)
        {
            return _uncommitted.Contains(block);
        }

        /// <summary>
        /// New production event is consistent if and only if none existing
        /// event comes from ancestry block.
        /// New consumption event is consistent if only if any existing
        /// production event comes from ancestry block, and not yet consumed
        /// in the chain determined by this production block.
        /// </summary>
        public bool IsAddConsistent(Span<CoinEvent> events, CoinEvent toAdd)
        {
            if (toAdd.Kind == CoinEventKind.Production)
            {
                foreach (var coinEvent in events)
                {
                    if (IsAncestor(toAdd.BlockAlias, coinEvent.BlockAlias))
                    {
                        return false;
                    }
                }

                return true;
            }

            bool hasBeenProduced = false;

            foreach (var coinEvent in events)
            {
                if (IsAncestor(toAdd.BlockAlias, coinEvent.BlockAlias))
                {
                    if (coinEvent.Kind == CoinEventKind.Production)
                    {
                        hasBeenProduced = true;
                    }
                    else
                    {
                        // already consumed in this chain.
                        return false;
                    }
                }
            }

            return hasBeenProduced;
        }

        public bool TryGetEventsInContext(Span<CoinEvent> events, BlockAlias context, out BlockAlias production,
            out BlockAlias consumption)
        {
            production = BlockAlias.Undefined;
            consumption = BlockAlias.Undefined;
            foreach (var coinEvent in events)
            {
                if (IsAncestor(context, coinEvent.BlockAlias))
                {
                    if (coinEvent.Kind == CoinEventKind.Production)
                    {
                        production = coinEvent.BlockAlias;
                    }
                    else
                    {
                        consumption = coinEvent.BlockAlias;
                    }
                }
            }

            return production.IsDefined || consumption.IsDefined;
        }

        /// <summary>
        /// Determine if a set of CoinEvent is eligible to be pruned.
        /// </summary>
        /// <remarks>
        /// Logic is:
        /// 1. If any production event or consumption event is too recent,
        /// we don't prune.
        /// 2. We loop over all events of payload, keep only those that
        /// lineage decides as <see cref="IsBlockPermanentlyOrphaned"/> = false.
        /// Only main chain events would survive normally, and the count would
        /// be less or equal to two.
        /// 3. Among survived events, if the set is empty, or there is a
        /// production and a consumption, we prune. Another scenario is that
        /// only a production event survived, then, we don't prune.
        /// </remarks>
        /// <returns>true if set is prunable, false otherwise</returns>
        public bool IsCoinPrunable(Span<CoinEvent> events)
        {
            var countProduction = 0;
            var countConsumption = 0;

            foreach (var coinEvent in events)
            {
                if (coinEvent.BlockAlias.BlockHeight > CoinPruneHeight)
                    return false; // too recent, don't prune

                if (IsBlockPermanentlyOrphaned(coinEvent.BlockAlias))
                    continue;

                switch (coinEvent.Kind)
                {
                    case CoinEventKind.Production:
                        countProduction++;
                        break;
                    case CoinEventKind.Consumption:
                        countConsumption++;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            return countConsumption == 0 && countProduction == 0 ||
                   countConsumption == 1 && countProduction == 1;
        }
    }
}