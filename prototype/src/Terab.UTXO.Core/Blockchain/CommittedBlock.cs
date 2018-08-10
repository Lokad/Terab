using System;
using Terab.UTXO.Core.Hash;

namespace Terab.UTXO.Core.Blockchain
{
    /// <summary>
    /// Represents a block in the blockchain for Terab purposes, meaning
    /// only the metadata important for Terab is stored.
    /// This block is committed meaning no more UTX(O)s can be added to it
    /// in the Sozu table.
    /// A block is identified by its Hash256 which is the same as the identifier
    /// of the block in the 'real' blockchain.
    /// </summary>
    public struct CommittedBlock : IBlock, IEquatable<CommittedBlock>
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
        /// The hash of a block.
        /// </summary>
        public BlockId BlockId { get; }

        public bool IsEmpty =>
            BlockHeight == 0 && Alias == BlockAlias.GenesisParent && Parent == BlockAlias.GenesisParent;

        public CommittedBlock(int blockHeight, BlockId blockId, BlockAlias alias, BlockAlias parent)
        {
            BlockId = blockId;
            BlockHeight = blockHeight;
            Alias = alias;
            Parent = parent;
        }

        public bool Equals(CommittedBlock other)
        {
            return BlockHeight.Equals(other.BlockHeight)
                   && Alias.Equals(other.Alias)
                   && Parent.Equals(other.Parent)
                   && BlockId.Equals(other.BlockId);
        }
    }
}