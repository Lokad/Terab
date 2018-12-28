// Copyright Lokad 2018 under MIT BCH.
using System.IO.MemoryMappedFiles;
using Terab.Lib.Chains;
using Terab.Lib.Messaging;
using Xunit;
using CommitBlockStatus = Terab.Lib.Chains.CommitBlockStatus;
using OpenBlockStatus = Terab.Lib.Chains.OpenBlockStatus;

namespace Terab.Lib.Tests.Chains
{
    public class ChainStoreTests
    {
        private IChainStore _store;

        private readonly CommittedBlockId _hash1 = GetMockBlockId(1);
        private readonly CommittedBlockId _hash2 = GetMockBlockId(2);
        private readonly CommittedBlockId _hash3 = GetMockBlockId(3);

        private UncommittedBlockId _tmpId1;
        private UncommittedBlockId _tmpId2;
        private UncommittedBlockId _tmpId3;
        private UncommittedBlockId _tmpId4;

        private BlockAlias _1 = new BlockAlias(1, 0);
        private BlockAlias _2 = new BlockAlias(2, 0);
        private BlockAlias _2_1 = new BlockAlias(2, 1);
        private BlockAlias _3 = new BlockAlias(3, 0);
        private BlockAlias _3_1 = new BlockAlias(3, 1);
        private BlockAlias _4 = new BlockAlias(4, 0);
        private BlockAlias _4_1 = new BlockAlias(4, 1);
        private BlockAlias _5 = new BlockAlias(5, 0);
        private BlockAlias _5_1 = new BlockAlias(5, 1);
        private BlockAlias _6 = new BlockAlias(6, 0);
        private BlockAlias _7 = new BlockAlias(7, 0);
        private BlockAlias _8 = new BlockAlias(8, 0);

        private static unsafe CommittedBlockId GetMockBlockId(byte filler)
        {
            var blockId = new CommittedBlockId();
            for (var i = 0; i < CommittedBlockId.SizeInBytes; i++)
                blockId.Data[i] = filler;

            return blockId;
        }

        [Fact]
        public void AddBlockToCommittedParent()
        {
            _store = new ChainStore(
                new MemoryMappedFileSlim(MemoryMappedFile.CreateNew(null, 10000)));

            var status1 = _store.TryOpenBlock(CommittedBlockId.GenesisParent, out var block1);
            Assert.True(OpenBlockStatus.Success.Equals(status1));

            var id = block1.Alias;
            var status2 = _store.TryCommitBlock(id, CommittedBlockId.Genesis, out _);

            Assert.Equal(CommitBlockStatus.Success, status2);
            Assert.Equal(BlockAlias.Genesis, id);

            var status3 = _store.TryOpenBlock(CommittedBlockId.Genesis, out _);
            Assert.Equal(OpenBlockStatus.Success, status3);
        }

        [Fact]
        public void AddValidBlocksWithFork()
        {
            _store = new ChainStore(
                new MemoryMappedFileSlim(MemoryMappedFile.CreateNew(null, 10000)));

            _store.TryOpenBlock(CommittedBlockId.GenesisParent, out var b0);
            var id0 = b0.Alias;
            // U(0)
            _store.TryCommitBlock(id0, CommittedBlockId.Genesis, out _);
            // C(0)
            _store.TryOpenBlock(CommittedBlockId.Genesis, out var b1);
            var id1 = b1.Alias;
            // C(0) -> U(1)
            _store.TryCommitBlock(id1, _hash1, out _);
            // C(0) -> C(1)
            Assert.Equal(_1, id1);
            _store.TryOpenBlock(_hash1, out var b2);
            var id2 = b2.Alias;
            // C(0) -> C(1) -> U(2)
            Assert.Equal(_2, id2);
            _store.TryCommitBlock(id2, _hash2, out _);
            // C(0) -> C(1) -> C(2)
            // Second child for second block
            _store.TryOpenBlock(_hash1, out var b21);
            var id21 = b21.Alias;
            // C(0) -> C(1) -> C(2)
            //             \-> U(2-1)
            Assert.Equal(_2_1, id21);
            _store.TryCommitBlock(id21, _hash3, out _);
            // C(0) -> C(1) -> C(2)
            //             \-> C(2-1)
        }

        [Fact]
        public void AddValidBlocksWithForkAllUncommitted()
        {
            _store = new ChainStore(
                new MemoryMappedFileSlim(MemoryMappedFile.CreateNew(null, 10000)));

            _store.TryOpenBlock(CommittedBlockId.GenesisParent, out var tmpBlock);
            // U(0)
            _tmpId1 = tmpBlock.BlockId;
            _store.TryCommitBlock(tmpBlock.Alias, CommittedBlockId.Genesis, out _);
            // C(0)
            _store.TryOpenBlock(CommittedBlockId.Genesis, out var tmp2Block);
            // C(0) -> U(1)
            Assert.Equal(tmp2Block.Alias, _1);
            _tmpId2 = tmp2Block.BlockId;
            _store.TryCommitBlock(tmp2Block.Alias, _hash1, out _);
            // C(0) -> C(1)
            _store.TryOpenBlock(_hash1, out var b1);
            Assert.Equal(_2, b1.Alias);
            _tmpId3 = b1.BlockId;
            // C(0) -> C(1) -> U(2)
            // Second child for second block
            _store.TryOpenBlock(_hash1, out b1);
            Assert.Equal(_2_1, b1.Alias);
            _tmpId4 = b1.BlockId;
            // C(0) -> C(1) -> U(2)
            //             \-> U(2-1)
        }

        [Fact]
        public void RetrieveBlock()
        {
            AddValidBlocksWithForkAllUncommitted();

            Assert.True(_store.TryGetCommittedBlock(BlockAlias.Genesis, out var genesisBlock));
            Assert.True(_store.TryGetCommittedBlock(_1, out var height1Block));
            Assert.True(_store.TryGetUncommittedBlock(_2, out var firstBlockU));
            Assert.True(_store.TryGetUncommittedBlock(_2_1, out var secondBlockU));

            Assert.Equal(CommittedBlockId.Genesis, genesisBlock.BlockId);
            Assert.Equal(_hash1, height1Block.BlockId);
            Assert.Equal(_tmpId3, firstBlockU.BlockId);
            Assert.Equal(_tmpId4, secondBlockU.BlockId);
            Assert.Equal(_1, firstBlockU.Parent);
            Assert.Equal(_1, secondBlockU.Parent);
            Assert.Equal(0, genesisBlock.BlockHeight);
            Assert.Equal(2, firstBlockU.BlockHeight);
            Assert.Equal(2, secondBlockU.BlockHeight);

            var status1 = _store.TryCommitBlock(_2, _hash1, out _);
            var status2 = _store.TryCommitBlock(_2_1, _hash2, out _);

            Assert.Equal(CommitBlockStatus.Success, status1);
            Assert.Equal(CommitBlockStatus.Success, status2);

            Assert.True(_store.TryGetCommittedBlock(_2, out var firstBlock));
            Assert.True(_store.TryGetCommittedBlock(_2_1, out var secondBlock));

            Assert.Equal(_hash1, firstBlock.BlockId);
            Assert.Equal(_hash2, secondBlock.BlockId);
            Assert.Equal(_1, firstBlock.Parent);
            Assert.Equal(_1, secondBlock.Parent);
            Assert.Equal(0, genesisBlock.BlockHeight);
            Assert.Equal(2, firstBlock.BlockHeight);
            Assert.Equal(2, secondBlock.BlockHeight);
        }

        [Fact]
        public void RetrieveAliasFromId()
        {
            AddValidBlocksWithFork();

            var status0 = _store.TryGetAlias(CommittedBlockId.Genesis, out var alias0);
            Assert.Equal(GetBlockAliasStatus.Success, status0);
            Assert.Equal(BlockAlias.Genesis, alias0);
            var status1 = _store.TryGetAlias(_hash1, out var alias1);
            Assert.Equal(GetBlockAliasStatus.Success, status1);
            Assert.Equal(_1, alias1);
            var status2 = _store.TryGetAlias(_hash2, out var alias2);
            Assert.Equal(GetBlockAliasStatus.Success, status2);
            Assert.Equal(_2, alias2);
            var status3 = _store.TryGetAlias(_hash3, out var alias3);
            Assert.Equal(GetBlockAliasStatus.Success, status3);
            Assert.Equal(_2_1, alias3);
            var status4 = _store.TryGetAlias(GetMockBlockId(42), out var alias4);
            Assert.Equal(GetBlockAliasStatus.BlockNotFound, status4);
            Assert.False(alias4.IsDefined);
        }

        [Fact]
        public void ReverseEnumerate()
        {
            AddValidBlocksWithFork();

            _store.TryGetAlias(_hash3, out var alias2_1);
            Assert.Equal(_2_1, alias2_1);

            _store.TryGetAlias(_hash2, out var alias2);
            Assert.Equal(_2, alias2);

            _store.TryGetAlias(_hash1, out var alias1);
            Assert.Equal(_1, alias1);

            _store.TryGetAlias(CommittedBlockId.Genesis, out var genesis);
            Assert.Equal(BlockAlias.Genesis, genesis);
        }
    }
}