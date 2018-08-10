using System;
using System.Collections.Generic;
using System.IO;
using Terab.UTXO.Core.Blockchain;
using Terab.UTXO.Core.Helpers;

namespace Terab.UTXO.Core.Serializer
{
    // TODO: [vermorel] I don't like the amount of manual serialization involved here.
    // I suggest to have the Lokad.StructuralSerializer open-sourced, and used as a 
    // Nuget for this very purpose.

    /// <summary>
    /// Permits serializing a given blockchain, and deserializing
    /// it from a given representation.
    /// </summary>
    public static class BlockchainSerializer
    {
        /// <summary>
        /// Reads blockchain information from a stream in the following order:
        ///  - number of blocks to read
        ///  - blocks with Hash256 representing the blockId first, then int
        ///         representing the parentAlias
        /// The blockHeight is calculated for each block based on the
        /// blockHeight of its parent, so it should not be stored.
        /// The corresponding Serialization method is <see cref="WriteTo"/>.
        /// </summary>
        /// <returns>A new list representing the blockchain.</returns>
        public static SimpleBlockchain ReadFrom(BinaryReader reader)
        {
            var committedBlocks = new List<CommittedBlock>();
            var uncommittedBlocks = new List<UncommittedBlock>();
            var maxBlockHeight = 0; // track the blockHeight so that it doesn't have to be recalculated afterward

            // Read blocks
            var committedBlockCount = reader.ReadInt32();
            for (var i = 0; i < committedBlockCount; i++)
            {
                var blockId = new BlockId(reader.ReadHash256());
                var blockAlias = new BlockAlias(reader.ReadUInt32());
                var parentAlias = new BlockAlias(reader.ReadUInt32());

                // Only the first block can have parentAlias 0.
                if (parentAlias == BlockAlias.GenesisParent && !blockId.Equals(BlockId.Genesis) ||
                    blockAlias.IsPrior(parentAlias))
                    throw new FormatException(
                        $"Parent alias {parentAlias} is an invalid parent for block {blockAlias}.");

                if (!TryFindCommitted(parentAlias, committedBlocks, out var parentBlock) &&
                    !blockId.Equals(BlockId.Genesis))
                    throw new ArgumentNullException($"No parent {parentAlias} found.");

                // The first block, which is the only one to have parent 0, has block height 0.
                var blockHeight = parentAlias == BlockAlias.GenesisParent
                    ? 0
                    : parentBlock.BlockHeight + 1;
                var newBlock = new CommittedBlock(blockHeight, blockId, blockAlias, parentAlias);

                if (blockHeight > maxBlockHeight)
                    maxBlockHeight = blockHeight;

                committedBlocks.Add(newBlock);
            }

            var numUncommmittedBlocks = reader.ReadInt32();
            for (var i = 0; i < numUncommmittedBlocks; i++)
            {
                var blockId = new TempBlockId(reader.ReadHash128());
                var blockAlias = new BlockAlias(reader.ReadUInt32());
                var parentAlias = new BlockAlias(reader.ReadUInt32());

                // Only the first block can have parentAlias 0.
                if ((uncommittedBlocks.Count != 0 || committedBlocks.Count != 0) &&
                    parentAlias == BlockAlias.GenesisParent || blockAlias.IsPrior(parentAlias))
                    throw new FormatException(
                        $"Parent alias {parentAlias} is an invalid parent for block {blockAlias}.");

                int blockHeight;
                if (TryFindCommitted(parentAlias, committedBlocks, out var parentBlock))
                    blockHeight = parentBlock.BlockHeight + 1;
                else
                {
                    if (uncommittedBlocks.Count == 0 && committedBlocks.Count == 0 &&
                        parentAlias == BlockAlias.GenesisParent) // we're adding the first block
                        blockHeight = 1;
                    else
                    {
                        if (!TryFindUncommitted(parentAlias, uncommittedBlocks, out var uParentBlock))
                            throw new ArgumentNullException($"No parent {parentAlias} found.");
                        blockHeight = uParentBlock.BlockHeight + 1;
                    }
                }

                var newBlock = new UncommittedBlock(blockHeight, blockId, blockAlias, parentAlias);

                if (blockHeight > maxBlockHeight)
                    maxBlockHeight = blockHeight;

                uncommittedBlocks.Add(newBlock);
            }

            return new SimpleBlockchain(committedBlocks, uncommittedBlocks, maxBlockHeight);
        }

        /// <summary>
        /// Writes the blockchain to a stream in a way that the
        /// <see cref="ReadFrom"/> method can extract it sensibly.
        /// </summary>
        public static void WriteTo(BinaryWriter writer, SimpleBlockchain chain)
        {
            writer.Write(chain.CommittedBlockCount);
            foreach (var block in chain.GetCommitted())
            {
                block.BlockId.WriteTo(writer);
                writer.Write(block.Alias.Value);
                writer.Write(block.Parent.Value);
            }

            writer.Write(chain.UncommittedBlockCount);
            foreach (var block in chain.GetUncommitted())
            {
                block.BlockId.WriteTo(writer);
                writer.Write(block.Alias.Value);
                writer.Write(block.Parent.Value);
            }
        }

        /// <summary>
        /// Find a given committed block in a chain of committed blocks that is
        /// enumerated from the end.
        /// </summary>
        /// <remarks>
        /// The enumerator of the blockchain cannot be used as the blockchain
        /// has not been created yet.
        /// </remarks>
        private static bool TryFindCommitted(BlockAlias alias, List<CommittedBlock> blocks,
            out CommittedBlock foundCommittedBlock)
        {
            foundCommittedBlock = blocks.FindLast(b => b.Alias == alias);
            return !foundCommittedBlock.Equals(default);
        }

        /// <summary>
        /// Find a given uncommitted block in a chain of uncommitted blocks that
        /// is enumerated from the end.
        /// </summary>
        /// <remarks>
        /// The enumerator of the blockchain cannot be used as the blockchain
        /// has not been created yet.
        /// </remarks>
        private static bool TryFindUncommitted(BlockAlias alias, List<UncommittedBlock> blocks,
            out UncommittedBlock foundUncommittedBlock)
        {
            foundUncommittedBlock = blocks.FindLast(b => b.Alias == alias);
            return !foundUncommittedBlock.Equals(default);
        }
    }
}