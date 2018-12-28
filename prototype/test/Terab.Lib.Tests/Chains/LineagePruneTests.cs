// Copyright Lokad 2018 under MIT BCH.
using System;
using Terab.Lib.Chains;
using Terab.Lib.Coins;
using Terab.Lib.Messaging;
using Terab.Lib.Tests.Mock;
using Xunit;

namespace Terab.Lib.Tests.Chains
{
    /// <summary>
    /// This class tests only pruning of <see cref="Lineage"/>.
    /// </summary>
    public class LineagePruneTests
    {
        // TODO: [vermorel] Unit tests should not include a state

        //private SimpleBlockchain _chain;

        private static readonly BlockAlias _1 = new BlockAlias(1,0);
        private static readonly BlockAlias _2 = new BlockAlias(2,0);
        private static readonly BlockAlias _2_1 = new BlockAlias(2,1);
        private static readonly BlockAlias _3 = new BlockAlias(3,0);
        private static readonly BlockAlias _3_1 = new BlockAlias(3,1);
        private static readonly BlockAlias _4 = new BlockAlias(4,0);
        private static readonly BlockAlias _5 = new BlockAlias(5,0);
        private static readonly BlockAlias _5_1 = new BlockAlias(5,1);

        private readonly CommittedBlockId _id_1 = GetMockBlockId(1),
            _id_2 = GetMockBlockId(2),
            _id_2_1 = GetMockBlockId(3),
            _id_3 = GetMockBlockId(4),
            _id_3_1 = GetMockBlockId(5),
            _id_4 = GetMockBlockId(6),
            _id_5_1 = GetMockBlockId(7),
            _id_5 = GetMockBlockId(8);


        private readonly ILineage _lineage;

        private static unsafe CommittedBlockId GetMockBlockId(byte filler)
        {
            var blockId = new CommittedBlockId();
            for (var i = 0; i < CommittedBlockId.SizeInBytes; i++)
                blockId.Data[i] = filler;

            return blockId;
        }

        // HACK: [vermorel] Constructor used as the setup of the unit tests, should be refactored.
        public LineagePruneTests()
        {
            var store = new VolatileChainStore();
            
            UncommittedBlock freshBlock;

            store.TryOpenBlock(CommittedBlockId.GenesisParent, out var b0);
            var id = b0.Alias;
            // U(0)
            store.TryCommitBlock(id, CommittedBlockId.Genesis, out _);
            // C(0)

            store.TryOpenBlock(CommittedBlockId.Genesis, out freshBlock);
            Assert.Equal(_1, freshBlock.Alias);
            // C(0) -> U(1)
            store.TryCommitBlock(_1, _id_1, out _);
            // C(0) -> C(1)
            store.TryOpenBlock(_id_1, out freshBlock);
            Assert.Equal(_2, freshBlock.Alias);
            // C(0) -> C(1) -> U(2)
            store.TryCommitBlock(_2, _id_2, out _);
            // C(0) -> C(1) -> C(2)
            // Second child for second block
            store.TryOpenBlock(_id_1, out freshBlock);
            Assert.Equal(_2_1, freshBlock.Alias);
            // C(0) -> C(1) -> C(2)
            //             \-> U(2-1)
            store.TryCommitBlock(_2_1, _id_2_1, out _);
            // C(0) -> C(1) -> C(2)
            //             \-> C(2-1)
            store.TryOpenBlock(_id_2, out freshBlock);
            Assert.Equal(_3, freshBlock.Alias);
            store.TryCommitBlock(_3, _id_3, out _);

            store.TryOpenBlock(_id_2_1, out freshBlock);
            Assert.Equal(_3_1, freshBlock.Alias);
            store.TryCommitBlock(_3_1, _id_3_1, out _);
            // C(0) -> C(1) -> C(2)   -> C(3)
            //             \-> C(2-1) -> C(3-1)

            store.TryOpenBlock(_id_3_1, out freshBlock);
            Assert.Equal(_4, freshBlock.Alias);
            store.TryCommitBlock(_4, _id_4, out _);

            store.TryOpenBlock(_id_4, out freshBlock);
            Assert.Equal(_5, freshBlock.Alias);
            store.TryCommitBlock(_5, _id_5, out _);

            store.TryOpenBlock(_id_4, out freshBlock);
            Assert.Equal(_5_1, freshBlock.Alias);
            store.TryCommitBlock(_5_1, _id_5_1, out _);

            // C(0) -> C(1) -> C(2)   -> C(3)
            //             \-> C(2-1) -> C(3-1) -> C(4) -> C(5)
            //                                        \ -> C(5-1)

            // pruning limit is _3_1
            var lineage = (Lineage)store.GetLineage();
            lineage.CoinPruneHeight = _3_1.BlockHeight;

            _lineage = lineage;
        }

        /// <summary>
        /// Case where production and consumption happened in "OLD" blocks
        /// of main chain.
        /// </summary>
        [Fact]
        public void PruneTest1()
        {
            Span<CoinEvent> events = stackalloc CoinEvent[3];
            events[0] = new CoinEvent(_1, CoinEventKind.Production);
            events[1] = new CoinEvent(_2, CoinEventKind.Consumption);
            events[2] = new CoinEvent(_2_1, CoinEventKind.Consumption);

            Assert.True(_lineage.IsCoinPrunable(events));
        }

        /// <summary>
        /// Case where consumption happened in side chain "OLD" blocks.
        /// </summary>
        [Fact]
        public void PruneTest2()
        {
            Span<CoinEvent> events = stackalloc CoinEvent[2];
            events[0] = new CoinEvent(_1, CoinEventKind.Production);
            events[1] = new CoinEvent(_2, CoinEventKind.Consumption);

            Assert.False(_lineage.IsCoinPrunable(events));
        }

        /// <summary>
        /// Case where consumption happened in too recent block of main chain.
        /// </summary>
        [Fact]
        public void PruneTest3()
        {
            Span<CoinEvent> events = stackalloc CoinEvent[2];
            events[0] = new CoinEvent(_1, CoinEventKind.Production);
            events[1] = new CoinEvent(_4, CoinEventKind.Consumption);

            Assert.False(_lineage.IsCoinPrunable(events));
        }

        /// <summary>
        /// Case where consumption happened in too recent block of side chain.
        /// </summary>
        [Fact]
        public void PruneTest4()
        {
            Span<CoinEvent> events = stackalloc CoinEvent[2];
            events[0] = new CoinEvent(_1, CoinEventKind.Production);
            events[1] = new CoinEvent(_5, CoinEventKind.Consumption);

            Assert.False(_lineage.IsCoinPrunable(events));
        }

        /// <summary>
        /// Case where production and consumption happened in side chain.
        /// </summary>
        [Fact]
        public void PruneTest5()
        {
            Span<CoinEvent> events = stackalloc CoinEvent[2];
            events[0] = new CoinEvent(_2, CoinEventKind.Production);
            events[1] = new CoinEvent(_3, CoinEventKind.Consumption);

            Assert.True(_lineage.IsCoinPrunable(events));
        }
    }
}
