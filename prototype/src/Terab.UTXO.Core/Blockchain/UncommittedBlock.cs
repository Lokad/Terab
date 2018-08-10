using System;
using Terab.UTXO.Core.Hash;

namespace Terab.UTXO.Core.Blockchain
{
    /// <summary>
    /// Represents an uncommitted block in the blockchain for Terab purposes,
    /// meaning only the metadata important for Terab is stored.
    /// The particularity of the uncommitted block is that it can still
    /// be added UTX(O)s in the Sozu table associated with this block
    /// and that it still does not have a fixed identifier.
    /// It is identified by a temporary GUID that Terab provides.
    /// </summary>
    public struct UncommittedBlock : IBlock, IEquatable<UncommittedBlock>
    {
        /// <summary>
        /// Height of a block in the blockchain. Multiple blocks can be of the
        /// same height but that means that they are not in the same branch.
        /// </summary>
        public int BlockHeight { get; }

        /// <summary>
        /// The alias of a block which identifies blocks uniquely within the
        /// Terab application. It is used to produce a block handle which
        /// is used for communication with the client.
        /// </summary>
        public BlockAlias Alias { get; }

        /// <summary>
        /// BlockAlias of the parent block.
        /// </summary>
        public BlockAlias Parent { get; }

        /// <summary>
        /// The temporary identifier of a block until it is committed.
        /// </summary>
        public TempBlockId BlockId { get; }

        public bool IsEmpty =>
            BlockHeight == 0 && Alias == BlockAlias.GenesisParent && Parent == BlockAlias.GenesisParent;

        public UncommittedBlock(int blockHeight, TempBlockId blockId, BlockAlias alias, BlockAlias parent)
        {
            BlockId = blockId;
            BlockHeight = blockHeight;
            Alias = alias;
            Parent = parent;
        }

        public bool Equals(UncommittedBlock other)
        {
            return BlockHeight.Equals(other.BlockHeight)
                   && Alias.Equals(other.Alias)
                   && Parent.Equals(other.Parent)
                   && BlockId.Equals(other.BlockId);
        }
    }
}