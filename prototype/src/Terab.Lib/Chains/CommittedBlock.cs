// Copyright Lokad 2018 under MIT BCH.
using Terab.Lib.Messaging;

namespace Terab.Lib.Chains
{
    /// <summary>
    /// For a committed block, no more UTX(O)s can be added in the Sozu table.
    /// A committed block is identified by the same Hash256 which is the
    /// identifier of the block in the 'real' blockchain.
    /// </summary>
    public struct CommittedBlock
    {
        public int BlockHeight => Alias.BlockHeight;

        public BlockAlias Alias { get; }

        public BlockAlias Parent { get; }

        /// <summary>
        /// The hash and unique identifier of a block.
        /// </summary>
        public CommittedBlockId BlockId { get; }

        public CommittedBlock(CommittedBlockId blockId, BlockAlias alias, BlockAlias parent)
        {
            BlockId = blockId;
            Alias = alias;
            Parent = parent;
        }
    }
}