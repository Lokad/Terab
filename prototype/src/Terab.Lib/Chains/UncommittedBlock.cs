// Copyright Lokad 2018 under MIT BCH.
using Terab.Lib.Messaging;

namespace Terab.Lib.Chains
{
    /// <summary>
    /// The uncommitted block serves as reference point to add UTX(O)s in the
    /// Sozu table associated with this block.
    /// As it does not have a fixed identifier, it is identified by
    /// <see cref="UncommittedBlockId"/>, a temporary GUID that Terab provides.
    /// </summary>
    public struct UncommittedBlock
    {
        public int BlockHeight => Alias.BlockHeight;

        public BlockAlias Alias { get; }

        public BlockAlias Parent { get; }

        /// <summary>
        /// The temporary identifier of a block until it is committed.
        /// </summary>
        public UncommittedBlockId BlockId { get; }

        public UncommittedBlock(UncommittedBlockId blockId, BlockAlias alias, BlockAlias parent)
        {
            BlockId = blockId;
            Alias = alias;
            Parent = parent;
        }
    }
}