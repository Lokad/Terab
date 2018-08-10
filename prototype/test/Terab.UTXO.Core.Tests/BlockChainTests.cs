using System;
using System.IO;
using Terab.UTXO.Core.Blockchain;
using Terab.UTXO.Core.Hash;
using Terab.UTXO.Core.Serializer;
using Xunit;

namespace Terab.UTXO.Core.Tests
{
    public class BlockchainTests
    {
        private SimpleBlockchain _chain;

        private readonly BlockId _hash1 =
            new BlockId(new Hash256(0x11111111UL, 0x22222222UL, 0x33333333UL, 0x44444444UL));

        private readonly BlockId _hash2 =
            new BlockId(new Hash256(0xFFFFFFFFUL, 0xEEEEEEEEUL, 0xDDDDDDDDUL, 0xCCCCCCCCUL));

        private readonly BlockId _hash3 =
            new BlockId(new Hash256(0x1111111122UL, 0x2222222233UL, 0x3333333344UL, 0x4444444455UL));

        private TempBlockId _tmpId1;
        private TempBlockId _tmpId2;
        private TempBlockId _tmpId3;
        private TempBlockId _tmpId4;

        private BlockAlias _1 = new BlockAlias(1);
        private BlockAlias _2 = new BlockAlias(2);
        private BlockAlias _3 = new BlockAlias(3);
        private BlockAlias _4 = new BlockAlias(4);
        private BlockAlias _5 = new BlockAlias(5);
        private BlockAlias _6 = new BlockAlias(6);
        private BlockAlias _7 = new BlockAlias(7);
        private BlockAlias _8 = new BlockAlias(8);


        [Fact]
        public void AddBlockToCommittedParent()
        {
            _chain = new SimpleBlockchain();
            var id = _chain.OpenFirstBlock().Alias;
            _chain.CommitBlock(id, BlockId.Genesis);

            _chain.OpenBlock(_1);
        }

        [Fact]
        public void AddBlockToUncommittedParent()
        {
            _chain = new SimpleBlockchain();
            _chain.OpenFirstBlock();
            _chain.OpenBlock(_1);
        }

        [Fact]
        public void CommitChildOfUncommittedParent()
        {
            _chain = new SimpleBlockchain();
            _chain.OpenFirstBlock();
            var id = _chain.OpenBlock(_1).Alias;
            Assert.Throws<ArgumentException>(() => _chain.CommitBlock(id, _hash1));
        }

        [Fact]
        public void AddValidBlocksWithFork()
        {
            _chain = new SimpleBlockchain();
            var id = _chain.OpenFirstBlock().Alias;
            Assert.Equal(0, _chain.BlockchainHeight);
            _chain.CommitBlock(id, BlockId.Genesis);
            Assert.Equal(0, _chain.BlockchainHeight);

            id = _chain.OpenBlock(_1).Alias;
            Assert.Equal(1, _chain.BlockchainHeight);
            _chain.CommitBlock(id, _hash1);
            Assert.Equal(1, _chain.BlockchainHeight);
            id = _chain.OpenBlock(_2).Alias;
            Assert.Equal(2, _chain.BlockchainHeight);
            _chain.CommitBlock(id, _hash2);
            Assert.Equal(2, _chain.BlockchainHeight);
            // Second child for second block
            id = _chain.OpenBlock(_2).Alias;
            Assert.Equal(2, _chain.BlockchainHeight);
            _chain.CommitBlock(id, _hash3);
            Assert.Equal(2, _chain.BlockchainHeight);
        }

        [Fact]
        public void AddValidBlocksWithForkAllUncommitted()
        {
            _chain = new SimpleBlockchain();
            _tmpId1 = _chain.OpenFirstBlock().BlockId;

            _tmpId2 = _chain.OpenBlock(_1).BlockId;
            _tmpId3 = _chain.OpenBlock(_2).BlockId;
            // Second child for second block
            _tmpId4 = _chain.OpenBlock(_2).BlockId;
        }

        [Fact]
        public void DeleteUncommittedBlock()
        {
            AddValidBlocksWithForkAllUncommitted();

            _chain.DeleteBlock(_3);
            Assert.Equal(2, _chain.BlockchainHeight);
            _chain.DeleteBlock(_4);
            Assert.Equal(1, _chain.BlockchainHeight);
        }

        [Fact]
        public void DeleteCommittedBlock()
        {
            AddValidBlocksWithFork();

            _chain.DeleteBlock(_3);
            Assert.Equal(2, _chain.BlockchainHeight);
            _chain.DeleteBlock(_4);
            Assert.Equal(1, _chain.BlockchainHeight);
        }

        [Fact]
        public void RetrieveBlock()
        {
            AddValidBlocksWithForkAllUncommitted();

            Assert.True(_chain.RetrieveUncommittedBlock(_1, out var genesisBlockU));
            Assert.True(_chain.RetrieveUncommittedBlock(_2, out var firstBlockU));
            Assert.True(_chain.RetrieveUncommittedBlock(_3, out var secondBlockU));
            Assert.True(_chain.RetrieveUncommittedBlock(_4, out var forkU));

            Assert.Equal(_tmpId1, genesisBlockU.BlockId);
            Assert.Equal(_tmpId2, firstBlockU.BlockId);
            Assert.Equal(_tmpId3, secondBlockU.BlockId);
            Assert.Equal(_tmpId4, forkU.BlockId);
            Assert.Equal(_1, firstBlockU.Parent);
            Assert.Equal(_2, secondBlockU.Parent);
            Assert.Equal(_2, forkU.Parent);
            Assert.Equal(0, genesisBlockU.BlockHeight);
            Assert.Equal(1, firstBlockU.BlockHeight);
            Assert.Equal(2, secondBlockU.BlockHeight);
            Assert.Equal(2, forkU.BlockHeight);

            _chain.CommitBlock(_1, BlockId.Genesis);
            _chain.CommitBlock(_2, _hash1);
            _chain.CommitBlock(_3, _hash2);
            _chain.CommitBlock(_4, _hash3);

            Assert.True(_chain.RetrieveCommittedBlock(_1, out var genesisBlock));
            Assert.True(_chain.RetrieveCommittedBlock(_2, out var firstBlock));
            Assert.True(_chain.RetrieveCommittedBlock(_3, out var secondBlock));
            Assert.True(_chain.RetrieveCommittedBlock(_4, out var fork));

            Assert.Equal(BlockId.Genesis, genesisBlock.BlockId);
            Assert.Equal(_hash1, firstBlock.BlockId);
            Assert.Equal(_hash2, secondBlock.BlockId);
            Assert.Equal(_hash3, fork.BlockId);
            Assert.Equal(_1, firstBlock.Parent);
            Assert.Equal(_2, secondBlock.Parent);
            Assert.Equal(_2, fork.Parent);
            Assert.Equal(0, genesisBlock.BlockHeight);
            Assert.Equal(1, firstBlock.BlockHeight);
            Assert.Equal(2, secondBlock.BlockHeight);
            Assert.Equal(2, fork.BlockHeight);
        }

        /* ------ head does not exist anymore in the blockchain ------
        [TestMethod]
        public void VerifyHead()
        {
            // Forking does not change the main chain
            AddValidBlocksWithFork();

            Assert.IsTrue(_chain.IsOnMainChain(3));
            Assert.IsFalse(_chain.IsOnMainChain(4));

            // Prolong the side chain so that it becomes the main chain
            Assert.AreEqual(5, _chain.OpenBlock(4, new Hash256(0x2211111122UL, 0x3322222233UL, 0x4433333344UL, 0x5544444455UL)));
            Assert.IsFalse(_chain.IsOnMainChain(3));
            Assert.IsTrue(_chain.IsOnMainChain(4));
            Assert.IsTrue(_chain.IsOnMainChain(5));
        }
        */

        /* ------ freezeLimit is not present anymore in simple blockchain ------
        [TestMethod]
        [ExpectedException(typeof(BlockFrozenException))]
        public void AddChildToOldParent()
        {
            AddValidBlocksWithFork();

            var lastLong = 0x5544444455UL;
            for (int i = 0; i < 101; i++)
            {
                Assert.AreEqual(i+5, _chain.OpenBlock(i+4, new Hash256(0x2211111122UL, 0x3322222233UL, 0x4433333344UL, lastLong + (ulong) i)));
            }
            
            Assert.IsFalse(_chain.IsOnMainChain(3));
            Assert.IsTrue(_chain.IsOnMainChain(4));
            Assert.IsTrue(_chain.IsOnMainChain(105));

            Assert.AreEqual(103, _chain.RetrieveBlock(105).BlockHeight);
            Assert.AreEqual(2, _chain.RetrieveBlock(3).BlockHeight);

            _chain.OpenBlock(3, new Hash256(0x2211111122UL, 0x3322222233UL, 0x4433333344UL, 0x5544444455UL));
        }
        */

        [Fact]
        public void RetrieveAliasFromId()
        {
            AddValidBlocksWithFork();

            var alias = _chain.RetrieveAlias(BlockId.Genesis);
            Assert.Equal(_1, alias);
            alias = _chain.RetrieveAlias(
                new BlockId(new Hash256(0x11111111UL, 0x22222222UL, 0x33333333UL, 0x44444444UL)));
            Assert.Equal(_2, alias);
            alias = _chain.RetrieveAlias(
                new BlockId(new Hash256(0xFFFFFFFFUL, 0xEEEEEEEEUL, 0xDDDDDDDDUL, 0xCCCCCCCCUL)));
            Assert.Equal(_3, alias);
            alias = _chain.RetrieveAlias(new BlockId(new Hash256(0x1111111122UL, 0x2222222233UL, 0x3333333344UL,
                0x4444444455UL)));
            Assert.Equal(_4, alias);
            alias = _chain.RetrieveAlias(new BlockId(new Hash256(0x1111234522UL, 0x2222222233UL, 0x3333333344UL,
                0x4444444455UL)));
            Assert.Equal(BlockAlias.Undefined, alias);
        }

        [Fact]
        public void ReadAndWriteStreamAllCommitted()
        {
            AddValidBlocksWithFork();
            var stream = new MemoryStream();
            var writer = new BinaryWriter(stream);
            var reader = new BinaryReader(stream);

            BlockchainSerializer.WriteTo(writer, _chain);
            stream.Seek(0, 0);
            var newChain = BlockchainSerializer.ReadFrom(reader);

            Assert.Equal(_1, newChain.RetrieveAlias(BlockId.Genesis));
            Assert.Equal(_2, newChain.RetrieveAlias(_hash1));
            Assert.Equal(_3, newChain.RetrieveAlias(_hash2));
            Assert.Equal(_4, newChain.RetrieveAlias(_hash3));

            Assert.True(newChain.RetrieveCommittedBlock(_1, out var oneBlock));
            Assert.True(newChain.RetrieveCommittedBlock(_2, out var twoBlock));
            Assert.True(newChain.RetrieveCommittedBlock(_3, out var threeBlock));
            Assert.True(newChain.RetrieveCommittedBlock(_4, out var fourBlock));

            Assert.Equal(BlockId.Genesis, oneBlock.BlockId);
            Assert.Equal(_hash1, twoBlock.BlockId);
            Assert.Equal(_hash2, threeBlock.BlockId);
            Assert.Equal(_hash3, fourBlock.BlockId);

            Assert.Equal(2, _chain.BlockchainHeight);
        }

        [Fact]
        public void ReadAndWriteStreamMixed()
        {
            AddValidBlocksWithForkAllUncommitted();
            _chain.CommitBlock(_1, BlockId.Genesis);
            _chain.CommitBlock(_2, _hash2);

            var stream = new MemoryStream();
            var writer = new BinaryWriter(stream);
            var reader = new BinaryReader(stream);

            BlockchainSerializer.WriteTo(writer, _chain);
            stream.Seek(0, 0);
            SimpleBlockchain newChain = BlockchainSerializer.ReadFrom(reader);

            Assert.Equal(_1, newChain.RetrieveAlias(BlockId.Genesis));
            Assert.Equal(_2, newChain.RetrieveAlias(_hash2));
            Assert.Equal(_3, newChain.RetrieveAlias(_tmpId3));
            Assert.Equal(_4, newChain.RetrieveAlias(_tmpId4));

            Assert.True(newChain.RetrieveCommittedBlock(_1, out var oneBlock));
            Assert.True(newChain.RetrieveCommittedBlock(_2, out var twoBlock));
            Assert.True(newChain.RetrieveUncommittedBlock(_3, out var threeBlock));
            Assert.True(newChain.RetrieveUncommittedBlock(_4, out var fourBlock));

            Assert.Equal(BlockId.Genesis, oneBlock.BlockId);
            Assert.Equal(_hash2, twoBlock.BlockId);
            Assert.Equal(_tmpId3, threeBlock.BlockId);
            Assert.Equal(_tmpId4, fourBlock.BlockId);

            Assert.Equal(2, _chain.BlockchainHeight);
        }

        [Fact]
        public void ReadAndWriteStreamAllUncommitted()
        {
            AddValidBlocksWithForkAllUncommitted();
            var stream = new MemoryStream();
            var writer = new BinaryWriter(stream);
            var reader = new BinaryReader(stream);

            BlockchainSerializer.WriteTo(writer, _chain);
            stream.Seek(0, 0);
            SimpleBlockchain newChain = BlockchainSerializer.ReadFrom(reader);

            Assert.Equal(_1, newChain.RetrieveAlias(_tmpId1));
            Assert.Equal(_2, newChain.RetrieveAlias(_tmpId2));
            Assert.Equal(_3, newChain.RetrieveAlias(_tmpId3));
            Assert.Equal(_4, newChain.RetrieveAlias(_tmpId4));

            Assert.True(newChain.RetrieveUncommittedBlock(_1, out var oneBlock));
            Assert.True(newChain.RetrieveUncommittedBlock(_2, out var twoBlock));
            Assert.True(newChain.RetrieveUncommittedBlock(_3, out var threeBlock));
            Assert.True(newChain.RetrieveUncommittedBlock(_4, out var fourBlock));

            Assert.Equal(_tmpId1, oneBlock.BlockId);
            Assert.Equal(_tmpId2, twoBlock.BlockId);
            Assert.Equal(_tmpId3, threeBlock.BlockId);
            Assert.Equal(_tmpId4, fourBlock.BlockId);

            Assert.Equal(2, _chain.BlockchainHeight);
        }

        [Fact]
        public void ReverseEnumerate()
        {
            AddValidBlocksWithFork();

            var revEnum = _chain.GetReverseEnumerator();
            var counter = 0;
            foreach (var _ in revEnum)
            {
                switch (counter)
                {
                    case 0:
                        Assert.Equal(_4, _chain.RetrieveAlias(_hash3));
                        break;
                    case 1:
                        Assert.Equal(_3, _chain.RetrieveAlias(_hash2));
                        break;
                    case 2:
                        Assert.Equal(_2, _chain.RetrieveAlias(_hash1));
                        break;
                    case 3:
                        Assert.Equal(_1, _chain.RetrieveAlias(BlockId.Genesis));
                        break;
                }

                counter++;
            }

            Assert.Equal(4, counter);
        }

        // TODO delete method not tested as it is not yet finished

        ///////////////////////////////////////////////
        // OptimizedLineage tests
        ///////////////////////////////////////////////

        [Fact]
        public void OptimizedLineageIsAncestor()
        {
            AddValidBlocksWithFork();

            var opti = new OptimizedLineage(new BlockchainStrategies().GetQuasiOrphans(_chain), _5);
            // 2 is the empty block
            Assert.False(opti.IsAncestor(_2, _3));
            Assert.False(opti.IsAncestor(_1, _3));
            Assert.True(opti.IsAncestor(_3, _1));
            Assert.True(opti.IsAncestor(_3, _3));
            // _4 is a fork to 3
            Assert.False(opti.IsAncestor(_3, _4));
            Assert.True(opti.IsAncestor(_4, _1));
            Assert.True(opti.IsAncestor(_4, BlockAlias.GenesisParent));
        }

        [Fact]
        public void OptimizedLineageIsPruneable()
        {
            AddValidBlocksWithFork();

            var id = _chain.OpenBlock(_3).Alias;
            _chain.CommitBlock(id, new BlockId(new Hash256(0x11UL, 0x13UL, 0x14UL, 0x15UL)));
            // Prolong pruneable fork
            id = _chain.OpenBlock(_4).Alias;
            _chain.CommitBlock(id, new BlockId(new Hash256(0x12UL, 0x13UL, 0x14UL, 0x15UL)));
            // Create new fork
            id = _chain.OpenBlock(_5).Alias;
            _chain.CommitBlock(id, new BlockId(new Hash256(0x13UL, 0x13UL, 0x14UL, 0x15UL)));
            id = _chain.OpenBlock(_5).Alias;
            _chain.CommitBlock(id, new BlockId(new Hash256(0x14UL, 0x13UL, 0x14UL, 0x15UL)));

            var opti = new OptimizedLineage(new BlockchainStrategies().GetQuasiOrphans(_chain), _3);

            // Is on main chain
            Assert.False(opti.IsPruneable(_3));
            Assert.False(opti.IsPruneable(_7));
            // Child of branch which is below limit
            Assert.True(opti.IsPruneable(_4));
            Assert.True(opti.IsPruneable(_6));
            // Child of too recent branch
            Assert.False(opti.IsPruneable(_8));
        }

        [Fact]
        public void CreateOptiEverythingPruneable()
        {
            AddValidBlocksWithFork();

            var id = _chain.OpenBlock(_3).Alias;
            _chain.CommitBlock(id, new BlockId(new Hash256(0x11UL, 0x13UL, 0x14UL, 0x15UL)));
            // Prolong pruneable fork
            id = _chain.OpenBlock(_4).Alias;
            _chain.CommitBlock(id, new BlockId(new Hash256(0x12UL, 0x13UL, 0x14UL, 0x15UL)));
            // Create new fork
            id = _chain.OpenBlock(_5).Alias;
            _chain.CommitBlock(id, new BlockId(new Hash256(0x13UL, 0x13UL, 0x14UL, 0x15UL)));
            id = _chain.OpenBlock(_5).Alias;
            _chain.CommitBlock(id, new BlockId(new Hash256(0x14UL, 0x13UL, 0x14UL, 0x15UL)));

            var opti = new OptimizedLineage(new BlockchainStrategies().GetQuasiOrphans(_chain), _3);

            // Is on main chain
            Assert.False(opti.IsPruneable(_3));
            Assert.False(opti.IsPruneable(_7));
            Assert.False(opti.IsPruneable(_2));
            // Child of branch which is below limit
            Assert.True(opti.IsPruneable(_4));
            Assert.True(opti.IsPruneable(_6));
            // still above limit
            Assert.False(opti.IsPruneable(_8));

            id = _chain.OpenBlock(_7).Alias;
            _chain.CommitBlock(id, new BlockId(new Hash256(0x15UL, 0x13UL, 0x14UL, 0x15UL)));
            opti = new OptimizedLineage(new BlockchainStrategies().GetQuasiOrphans(_chain), _7);

            // passed below limit
            Assert.True(opti.IsPruneable(_8));
        }

        [Fact]
        public void CreateOptiNothingPruneable()
        {
            AddValidBlocksWithFork();
            var id = _chain.OpenBlock(_3).Alias;
            _chain.CommitBlock(id, new BlockId(new Hash256(0x11UL, 0x13UL, 0x14UL, 0x15UL)));
            // Prolong pruneable fork
            id = _chain.OpenBlock(_4).Alias;
            _chain.CommitBlock(id, new BlockId(new Hash256(0x12UL, 0x13UL, 0x14UL, 0x15UL)));
            // Create new fork
            id = _chain.OpenBlock(_5).Alias;
            _chain.CommitBlock(id, new BlockId(new Hash256(0x13UL, 0x13UL, 0x14UL, 0x15UL)));
            id = _chain.OpenBlock(_5).Alias;
            _chain.CommitBlock(id, new BlockId(new Hash256(0x14UL, 0x13UL, 0x14UL, 0x15UL)));

            var opti = new OptimizedLineage(new BlockchainStrategies().GetQuasiOrphans(_chain), BlockAlias.Genesis);

            // Is on main chain
            Assert.False(opti.IsPruneable(_3));
            Assert.False(opti.IsPruneable(_7));
            // Everything is above limit
            Assert.False(opti.IsPruneable(_4));
            Assert.False(opti.IsPruneable(_6));
            Assert.False(opti.IsPruneable(_8));
        }
    }
}