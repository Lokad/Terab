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
    /// This class tests <see cref="Lineage"/> except pruning.
    /// </summary>
    public class LineageTests
    {
        // the expected aliases of the blocks we'll be working with in this test class:
        private static readonly BlockAlias _1 = new BlockAlias(1, 0);
        private static readonly BlockAlias _2_0 = new BlockAlias(2, 0);
        private static readonly BlockAlias _2_1 = new BlockAlias(2,1);

        private static readonly CommittedBlockId _id_1 = GetMockBlockId(1);
        private static readonly CommittedBlockId _id_2_0 = GetMockBlockId(2);
        private static readonly CommittedBlockId _id_2_1 = GetMockBlockId(3);

        private readonly ILineage _lineage;

        private static unsafe CommittedBlockId GetMockBlockId(byte filler)
        {
            var blockId = new CommittedBlockId();
            for (var i = 0; i < CommittedBlockId.SizeInBytes; i++)
                blockId.Data[i] = filler;

            return blockId;
        }

        // HACK: [vermorel] Constructor used as the setup of the unit tests, should be refactored.
        public LineageTests()
        {
            var store = new VolatileChainStore();

            //var chain = new SimpleBlockchain();
            store.TryOpenBlock(CommittedBlockId.GenesisParent, out var b0);
            var id = b0.Alias;
            // U(0)
            store.TryCommitBlock(id, CommittedBlockId.Genesis, out _);
            // C(0)
            store.TryOpenBlock(CommittedBlockId.Genesis, out var b1);
            Assert.Equal(_1, b1.Alias);
            // C(0) -> U(1)
            store.TryCommitBlock(_1, _id_1, out _);
            // C(0) -> C(1)
            store.TryOpenBlock(_id_1, out var b2);
            Assert.Equal(_2_0, b2.Alias);
            // C(0) -> C(1) -> U(2)
            store.TryCommitBlock(_2_0, _id_2_0, out _);
            // C(0) -> C(1) -> C(2)
            // Second child for second block
            store.TryOpenBlock(_id_1, out var b21);
            Assert.Equal(_2_1, b21.Alias);
            // C(0) -> C(1) -> C(2)
            //             \-> U(2-1)
            store.TryCommitBlock(_2_1, _id_2_1, out _);
            // C(0) -> C(1) -> C(2)
            //             \-> C(2-1)

            var lineage = (Lineage)store.GetLineage();
            lineage.CoinPruneHeight = _1.BlockHeight;

            _lineage = lineage;
        }

        /// <summary>
        /// Case where coin has been consumed in both chain.
        /// </summary>
        [Fact]
        public void ReadConsistentTest1()
        {
            Span<CoinEvent> events = stackalloc CoinEvent[3];
            events[0] = new CoinEvent(_1, CoinEventKind.Production);
            events[1] = new CoinEvent(_2_0, CoinEventKind.Consumption);
            events[2] = new CoinEvent(_2_1, CoinEventKind.Consumption);

            var hasEvents = _lineage.TryGetEventsInContext(events, _2_0, out var production, out var consumption);
            Assert.Equal(production, _1);
            Assert.Equal(consumption, _2_0);
            Assert.True(hasEvents);
        }

        /// <summary>
        /// Case where coin has been consumed in one chain, user reads
        /// from side chain.
        /// </summary>
        [Fact]
        public void ReadConsistentTest2()
        {
            Span<CoinEvent> events = stackalloc CoinEvent[2];
            events[0] = new CoinEvent(_1, CoinEventKind.Production);
            events[1] = new CoinEvent(_2_0, CoinEventKind.Consumption);

            var hasEvents = _lineage.TryGetEventsInContext(events, _2_1, out var production, out var consumption);
            Assert.Equal(production, _1);
            Assert.False(consumption.IsDefined);
            Assert.True(hasEvents);
        }

        /// <summary>
        /// Case where coin has been produced in one chain, user reads
        /// from side chain.
        /// </summary>
        [Fact]
        public void ReadConsistentTest3()
        {
            Span<CoinEvent> events = stackalloc CoinEvent[1];
            events[0] = new CoinEvent(_2_0, CoinEventKind.Production);

            var hasEvents = _lineage.TryGetEventsInContext(events, _2_1, out var production, out var consumption);
            Assert.False(production.IsDefined);
            Assert.False(consumption.IsDefined);
            Assert.False(hasEvents);
        }

        /// <summary>
        /// Test double production failure case and success consumption case.
        /// </summary>
        [Fact]
        public void AddConsistentTest1()
        {
            Span<CoinEvent> events = stackalloc CoinEvent[1];
            events[0] = new CoinEvent(_1, CoinEventKind.Production);
            
            var failProduction = new CoinEvent(_2_0, CoinEventKind.Production);
            Assert.False(_lineage.IsAddConsistent(events, failProduction));

            var consumption = new CoinEvent(_2_0, CoinEventKind.Consumption);
            Assert.True(_lineage.IsAddConsistent(events, consumption));
        }

        /// <summary>
        /// Test consumption in side chain.
        /// </summary>
        [Fact]
        public void AddConsistentTest2()
        {
            Span<CoinEvent> events = stackalloc CoinEvent[2];
            events[0] = new CoinEvent(_1, CoinEventKind.Production);
            events[1] = new CoinEvent(_2_0, CoinEventKind.Consumption);

            var consumption = new CoinEvent(_2_1, CoinEventKind.Consumption);
            Assert.True(_lineage.IsAddConsistent(events, consumption));
        }

        /// <summary>
        /// Test double consumption failure.
        /// </summary>
        [Fact]
        public void AddConsistentTest3()
        {
            Span<CoinEvent> events = stackalloc CoinEvent[2];
            events[0] = new CoinEvent(_1, CoinEventKind.Production);
            events[1] = new CoinEvent(_1, CoinEventKind.Consumption);

            var consumption = new CoinEvent(_2_0, CoinEventKind.Consumption);
            Assert.False(_lineage.IsAddConsistent(events, consumption));
        }

        /// <summary>
        /// Test double production in different chains.
        /// Test also prevent consumption of coin not produced in same chain.
        /// </summary>
        [Fact]
        public void AddConsistentTest4()
        {
            Span<CoinEvent> events = stackalloc CoinEvent[1];
            events[0] = new CoinEvent(_2_0, CoinEventKind.Production);

            var consumption = new CoinEvent(_2_1, CoinEventKind.Consumption);
            Assert.False(_lineage.IsAddConsistent(events, consumption));

            var production = new CoinEvent(_2_1, CoinEventKind.Production);
            Assert.True(_lineage.IsAddConsistent(events, production));
        }

        /// <summary>
        /// Test add production idempotence.
        /// </summary>
        [Fact]
        public void AddConsistencetTest5()
        {
            Span<CoinEvent> events = stackalloc CoinEvent[1];
            var ev = new CoinEvent(_2_0, CoinEventKind.Production);
            events[0] = ev;

            Assert.False(_lineage.IsAddConsistent(events, ev));
        }

        /// <summary>
        /// Test add consumption idempotence.
        /// </summary>
        [Fact]
        public void AddConsistencetTest6()
        {
            Span<CoinEvent> events = stackalloc CoinEvent[2];
            events[0] = new CoinEvent(_2_0, CoinEventKind.Production);
            var ev = new CoinEvent(_2_0, CoinEventKind.Consumption);
            events[1] = ev;

            Assert.False(_lineage.IsAddConsistent(events, ev));
        }
    }
}
