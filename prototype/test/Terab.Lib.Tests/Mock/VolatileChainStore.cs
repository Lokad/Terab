// Copyright Lokad 2018 under MIT BCH.
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Terab.Lib.Chains;
using Terab.Lib.Messaging;
using CommitBlockStatus = Terab.Lib.Chains.CommitBlockStatus;
using OpenBlockStatus = Terab.Lib.Chains.OpenBlockStatus;

namespace Terab.Lib.Tests.Mock
{
    public class VolatileChainStore : IChainStore
    {
        private const int TestChainStoreSize = 1024 * 1024;

        private const byte CurrentVersion = 1;

        /// <summary>
        /// For most methods, we only need to scan the recent blocks.
        /// </summary>
        private const int BlockScanLimit = 1000;

        /// <summary> Setting (rather than constant) intended for unit testing purposes. </summary>
        public int BlockPruneLimitDistance { get; set; } = Constants.BlockPruneLimitDistance;

        private readonly Memory<byte> _memory;

        private ILog _log;

        [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 64)]
        private unsafe struct Header
        {
            public static int SizeInBytes => sizeof(Header);

            public byte Version;
            public int BlockCount;
        }

        [StructLayout(LayoutKind.Explicit, Pack = 1, Size = 64)]
        private struct Block
        {
            [FieldOffset(0)] public bool IsCommitted;

            // TODO: [vermorel] no logic yet to take advantage of of 'IsDeleted' in the chain store
            [FieldOffset(1)] public bool IsDeleted;

            [FieldOffset(2)] public BlockAlias Parent;

            [FieldOffset(6)] public BlockAlias Alias;

            [FieldOffset(10)] public UncommittedBlockId UncommittedBlockId;

            // yes, '10' again on purpose
            [FieldOffset(10)] public CommittedBlockId BlockId;

            public int BlockHeight => Alias.BlockHeight;
        }

        public VolatileChainStore(ILog log = null)
        {
            _memory = new Memory<byte>(new byte[TestChainStoreSize]);
        }

        private ref Header AsHeader => ref MemoryMarshal.Cast<byte, Header>(_memory.Span)[0];

        private Span<Block> GetBlocks() => MemoryMarshal.Cast<byte, Block>(_memory.Span.Slice(Header.SizeInBytes));

        public void Initialize()
        {
           
        }

        public OpenBlockStatus TryOpenBlock(CommittedBlockId parent, out UncommittedBlock freshBlock)
        {
            var blocks = GetBlocks();
            var blockCount = AsHeader.BlockCount;
            var lowerIndex = Math.Max(0, blockCount - BlockScanLimit);

            freshBlock = default;

            if (blockCount == 0 && !parent.Equals(CommittedBlockId.GenesisParent))
                return OpenBlockStatus.ParentNotFound;

            // Find the parent, and assess the block height
            int parentBlockHeight = -1;
            var parentAlias = BlockAlias.Undefined;
            for (var i = blockCount - 1; i >= lowerIndex; i--)
            {
                ref var candidate = ref blocks[i];
                if (candidate.IsDeleted)
                    continue;

                if (candidate.BlockId.Equals(parent))
                {
                    parentBlockHeight = candidate.BlockHeight;
                    parentAlias = candidate.Alias;
                    break;
                }

                if (i == 0)
                    return OpenBlockStatus.ParentNotFound;
            }

            if (!parent.Equals(CommittedBlockId.GenesisParent) && parentBlockHeight == -1)
                return OpenBlockStatus.ParentNotFound;

            if (parentBlockHeight == -1)
                parentAlias = BlockAlias.GenesisParent;

            // Block height goes through simple increments
            var blockHeight = parentBlockHeight + 1;

            // Find the subIndex
            var subIndex = 0;
            for (var i = blockCount - 1; i >= lowerIndex; i--)
            {
                if (blocks[i].BlockHeight == blockHeight)
                    subIndex = Math.Max(blocks[i].Alias.SubIndex + 1, subIndex);
            }

            var newBlock = new Block
            {
                Parent = parentAlias,
                Alias = new BlockAlias(blockHeight, subIndex),
                UncommittedBlockId = UncommittedBlockId.Create()
            };

            blocks[blockCount] = newBlock;

            AsHeader.BlockCount = blockCount + 1;

            freshBlock = new UncommittedBlock(newBlock.UncommittedBlockId, newBlock.Alias, parentAlias);
            return OpenBlockStatus.Success;
        }

        public CommitBlockStatus TryCommitBlock(BlockAlias alias, CommittedBlockId blockId, out CommittedBlock block)
        {
            var blocks = GetBlocks();
            var blockCount = AsHeader.BlockCount;
            var lowerIndex = Math.Max(0, blockCount - BlockScanLimit);

            block = default;

            if (alias.IsUndefined)
                return CommitBlockStatus.BlockNotFound;

            for (var i = blockCount - 1; i >= lowerIndex; i--)
            {
                if (blocks[i].Alias == alias)
                {
                    if (blocks[i].IsDeleted)
                        continue;

                    if (blocks[i].IsCommitted)
                    {
                        if (blocks[i].BlockId.Equals(blockId))
                        {
                            block = new CommittedBlock(blockId, alias, blocks[i].Parent);
                            return CommitBlockStatus.Success;
                        }

                        return CommitBlockStatus.BlockIdMismatch;
                    }

                    blocks[i].IsCommitted = true;
                    blocks[i].BlockId = blockId;

                    block = new CommittedBlock(blockId, alias, blocks[i].Parent);
                    return CommitBlockStatus.Success;
                }
            }

            return CommitBlockStatus.BlockNotFound;
        }

        public GetBlockAliasStatus TryGetAlias(UncommittedBlockId uncommittedBlockId, out BlockAlias alias)
        {
            var blocks = GetBlocks();
            var blockCount = AsHeader.BlockCount;

            for (var i = blockCount - 1; i >= 0; i--)
            {
                if (blocks[i].IsDeleted)
                    continue;

                if (blocks[i].UncommittedBlockId.Equals(uncommittedBlockId))
                {
                    alias = blocks[i].Alias;
                    return GetBlockAliasStatus.Success;
                }
            }

            alias = BlockAlias.Undefined;
            return GetBlockAliasStatus.BlockNotFound;
        }

        public GetBlockAliasStatus TryGetAlias(CommittedBlockId blockId, out BlockAlias alias)
        {
            var blocks = GetBlocks();
            var blockCount = AsHeader.BlockCount;

            for (var i = blockCount - 1; i >= 0; i--)
            {
                if (blocks[i].IsDeleted)
                    continue;

                if (blocks[i].BlockId.Equals(blockId))
                {
                    alias = blocks[i].Alias;
                    return GetBlockAliasStatus.Success;
                }
            }

            alias = BlockAlias.Undefined;
            return GetBlockAliasStatus.BlockNotFound;
        }

        public bool TryGetCommittedBlock(BlockAlias alias, out CommittedBlock block)
        {
            var blocks = GetBlocks();
            var blockCount = AsHeader.BlockCount;

            for (var i = blockCount - 1; i >= 0; i--)
            {
                if (blocks[i].IsDeleted || !blocks[i].IsCommitted)
                    continue;

                if (blocks[i].Alias == alias)
                {
                    block = new CommittedBlock(
                        blocks[i].BlockId, blocks[i].Alias, blocks[i].Parent);
                    return true;
                }
            }

            block = default;
            return false;
        }

        public bool TryGetUncommittedBlock(BlockAlias alias, out UncommittedBlock uncommittedBlock)
        {
            var blocks = GetBlocks();
            var blockCount = AsHeader.BlockCount;

            for (var i = blockCount - 1; i >= 0; i--)
            {
                if (blocks[i].IsDeleted || blocks[i].IsCommitted)
                    continue;

                if (blocks[i].Alias == alias)
                {
                    uncommittedBlock = new UncommittedBlock(
                        blocks[i].UncommittedBlockId, blocks[i].Alias, blocks[i].Parent);
                    return true;
                }
            }

            uncommittedBlock = default;
            return false;
        }

        public ILineage GetLineage()
        {
            var blocks = GetBlocks();
            var blockCount = AsHeader.BlockCount;

            // PERF: [vermorel] 'GetLineage' would benefit from block sweeps within the Sozu table.
            // The performance of the lineage decreases with the number of blocks that are not part.
            // of the main chain.
            //
            // With sweeps within the Sozu table which would guarantee that all entries associated to
            // orphaned blocks have been removed, 'lowerIndex' could be increased as the lower bound
            // to look for uncommitted blocks.
            var lowerIndex = 0;

            var committed = new List<CommittedBlock>(blockCount);
            var uncommitted = new List<UncommittedBlock>();

            for (var i = 0; i < blockCount; i++)
            {
                if (blocks[i].IsDeleted)
                    continue;

                if (blocks[i].IsCommitted)
                {
                    committed.Add(new CommittedBlock(
                        blocks[i].BlockId, blocks[i].Alias, blocks[i].Parent));
                }
                else if (i >= lowerIndex)
                {
                    uncommitted.Add(new UncommittedBlock(
                        blocks[i].UncommittedBlockId, blocks[i].Alias, blocks[i].Parent));
                }
            }

            return new Lineage(committed, uncommitted, BlockPruneLimitDistance);
        }
    }
}
