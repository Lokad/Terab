// Copyright Lokad 2018 under MIT BCH.
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using Moq;
using Terab.Lib.Chains;
using Terab.Lib.Coins;
using Terab.Lib.Messaging;
using Terab.Lib.Tests.Mock;
using Xunit;

namespace Terab.Lib.Tests.Coins
{
    public unsafe class SozuTableTests
    {
        private static ICoinStore GetSozuTable(IPackStore store, IOutpointHash hash)
        {
            return new SozuTable(store, hash);
        }

        private static Coin GetCoin(Random rand, BlockAlias blockAlias)
        {
            return GetCoin(rand, (byte) rand.Next(), blockAlias);
        }

        private static Coin GetCoin(Random rand, int scriptLength, BlockAlias blockAlias)
        {
            var coin = new Coin(new byte[4096]);

            var outpoint = new Outpoint();
            for (var i = 0; i < 32; i++)
                outpoint.TxId[i] = (byte) rand.Next();
            outpoint.TxIndex = rand.Next();

            coin.Outpoint = outpoint;

            var events = new[] {new CoinEvent(blockAlias, CoinEventKind.Production)};
            coin.SetEvents(events);

            var script = new byte[scriptLength];
            rand.NextBytes(script);

            var payload = new Payload(new byte[4096]);
            payload.NLockTime = (uint)rand.Next();
            payload.Satoshis = (ulong) rand.Next();
            payload.Append(script);

            coin.SetPayload(payload);

            return coin;
        }

        private static Coin GetCoin(Random rand)
        {
            var scriptLength = (byte) rand.Next();
            var coin = new Coin(new byte[4096]);

            var outpoint = new Outpoint();
            for (var i = 0; i < 32; i++)
                outpoint.TxId[i] = (byte)rand.Next();
            outpoint.TxIndex = rand.Next();

            coin.Outpoint = outpoint;

            var script = new byte[scriptLength];
            rand.NextBytes(script);

            var payload = new Payload(new byte[4096]);
            payload.NLockTime = (uint)rand.Next();
            payload.Satoshis = (ulong)rand.Next();
            payload.Append(script);

            coin.SetPayload(payload);

            return coin;
        }

        public static Mock<IOutpointHash> GetMockHash()
        {
            var mockHash = new Mock<IOutpointHash>();
            mockHash.Setup(x => x.Hash(ref It.Ref<Outpoint>.IsAny))
                .Returns((Outpoint p) => BinaryPrimitives.ReadUInt64BigEndian(new ReadOnlySpan<byte>(p.TxId, 8)));

            return mockHash;
        }

        [Fact]
        public void TryGetTest()
        {
            var volatileStore = new VolatilePackStore(1, new[] { 4096 });
            var hash = GetMockHash().Object;
            var rand = new Random(2);

            var pack = volatileStore.Read(0, 0);
            pack.Append(GetCoin(rand, new BlockAlias(123, 0)));
            pack.Append(GetCoin(rand, new BlockAlias(123, 0)));
            var coin1 = GetCoin(rand, new BlockAlias(123, 0));
            pack.Append(coin1);
            var context = new BlockAlias(123, 0);

            Coin coin2;
            var sozu = GetSozuTable(volatileStore, hash);
            bool res = sozu.TryGet(hash.Hash(ref coin1.Outpoint), ref coin1.Outpoint, context,
                new MockLineage((out BlockAlias result) => result = context, MockLineage.Undefined),
                out coin2, out var production, out var consumption);

            Assert.True(res, "Sozu2 TryGet failed to get an existing outpoint.");
            Assert.True(coin1.Span.SequenceEqual(coin2.Span), "Sozu2 TryGet mismatch.");
            Assert.Equal(production, context);
            Assert.False(consumption.IsDefined);

            Coin coin3 = GetCoin(rand);
            res = sozu.TryGet(hash.Hash(ref coin3.Outpoint), ref coin3.Outpoint, context, new MockLineage(),
                out coin2, out var production3, out var consumption3);
            Assert.False(res, "Sozu2 TryGet returns an nonexistent outpoint.");
        }

        [Fact]
        public void AddProductionThenOverflowTest()
        {
            var volatileStore = new VolatilePackStore(1, new[] {4096, 4096});
            var hash = GetMockHash().Object;
            var rand = new Random(2);

            var sozu = GetSozuTable(volatileStore, hash);

            int cumulSize = 0;
            do
            {
                var coin = GetCoin(rand);
                var context = new BlockAlias(123, 0);
                var ret = sozu.AddProduction(hash.Hash(ref coin.Outpoint), ref coin.Outpoint, false, coin.Payload,
                    context,
                    new MockLineage());

                Assert.Equal(CoinChangeStatus.Success, ret);
                cumulSize += coin.SizeInBytes;
            } while (cumulSize <= 4096);

            var p0 = volatileStore.Read(0, 0);
            Assert.True(p0.OutpointSigCount > 0);
        }

        /// <summary>
        /// Test the case that pruning prevents effective overflow.
        /// </summary>
        [Fact]
        public void OverflowThenPruneTest()
        {
            var hash = GetMockHash().Object;
            var rand = new Random(2);
            var volatileStore = new VolatilePackStore(1, new[] { 4096, 4096 });
            var sozu = GetSozuTable(volatileStore, hash);
            var lineage = new MockLineage();
            lineage.PruningActive = true;

            int cumulSize = 0;
            for (var i = 0; i < 3; i++)
            {
                var coin1 = GetCoin(rand);
                var ret1 = sozu.AddProduction(hash.Hash(ref coin1.Outpoint), ref coin1.Outpoint, false, coin1.Payload,
                    MockLineage.CoinEventToPrune.BlockAlias, lineage);
                Assert.Equal(CoinChangeStatus.Success, ret1);
                cumulSize += coin1.SizeInBytes;
            }

            do
            {
                var coin = GetCoin(rand);
                var context = new BlockAlias(123, 0);
                var ret = sozu.AddProduction(hash.Hash(ref coin.Outpoint), ref coin.Outpoint, false, coin.Payload,
                    context, lineage);

                Assert.Equal(CoinChangeStatus.Success, ret);
                cumulSize += coin.SizeInBytes;
            } while (cumulSize <= 4096);

            var p0 = volatileStore.Read(0, 0);
            Assert.True(p0.OutpointSigCount == 0);
            var p1 = volatileStore.Read(1, 0);
            Assert.True(p1.CoinCount == 0);
        }

        /// <summary>
        /// Test remove <see cref="OutpointSig"/> after pruning.
        /// We need first to overflow to second layer, and prune some
        /// outpoints in second layer.
        /// </summary>
        [Fact]
        public void RemoveSigsAfterPruningTest()
        {
            var hash = GetMockHash().Object;
            var rand = new Random(2);
            var volatileStore = new VolatilePackStore(1, new[] { 4096, 4096 });
            var sozu = GetSozuTable(volatileStore, hash);
            var lineage = new MockLineage();
            var pruneCount = 3;

            int cumulSize = 0;
            for (var i = 0; i < pruneCount; i++)
            {
                var coin1 = GetCoin(rand);
                var ret1 = sozu.AddProduction(hash.Hash(ref coin1.Outpoint), ref coin1.Outpoint, false, coin1.Payload,
                    MockLineage.CoinEventToPrune.BlockAlias, lineage);
                Assert.Equal(CoinChangeStatus.Success, ret1);
                cumulSize += coin1.SizeInBytes;
            }

            do
            {
                var coin = GetCoin(rand);
                var context = new BlockAlias(123, 0);
                var ret = sozu.AddProduction(hash.Hash(ref coin.Outpoint), ref coin.Outpoint, false, coin.Payload,
                    context, lineage);

                Assert.Equal(CoinChangeStatus.Success, ret);
                cumulSize += coin.SizeInBytes;
            } while (cumulSize <= 4096);

            var p0 = volatileStore.Read(0, 0);
            cumulSize = p0.SizeInBytes;
            int sigsCount = 0;
            int opCount = 0;
            do
            {
                var coin = GetCoin(rand);
                var context = new BlockAlias(123, 0);
                if (cumulSize + coin.SizeInBytes > 4096)
                {
                    p0 = volatileStore.Read(0, 0);
                    sigsCount = p0.OutpointSigCount;
                    opCount = p0.CoinCount + 1;
                    lineage.PruningActive = true;
                }

                var ret = sozu.AddProduction(hash.Hash(ref coin.Outpoint), ref coin.Outpoint, false, coin.Payload,
                    context, lineage);

                Assert.Equal(CoinChangeStatus.Success, ret);
                cumulSize += coin.SizeInBytes;
            } while (cumulSize <= 4096);

            p0 = volatileStore.Read(0, 0);
            Assert.True(opCount - p0.CoinCount + sigsCount - p0.OutpointSigCount == pruneCount);
        }

        [Fact]
        public void OverflowToLastLayerTest()
        {
            var volatileStore = new VolatilePackStore(1, new[] { 4096, 4096 });
            var hash = GetMockHash().Object;
            var rand = new Random(2);

            var sozu = GetSozuTable(volatileStore, hash);

            int cumulSize = 0;
            do
            {
                var coin = GetCoin(rand);
                var context = new BlockAlias(123, 0);
                var ret = sozu.AddProduction(hash.Hash(ref coin.Outpoint), ref coin.Outpoint, false, coin.Payload,
                    context,
                    new MockLineage());

                Assert.Equal(CoinChangeStatus.Success, ret);
                cumulSize += coin.SizeInBytes;
            } while (cumulSize <= 4096 * 2);

            var p0 = volatileStore.Read(0, 0);
            Assert.True(p0.OutpointSigCount > 0, "First layer doesn't have probabilistic filter.");

            var p1 = volatileStore.Read(1, 0);

            var pl = volatileStore.Read(volatileStore.LayerCount - 1, 0);
            Assert.True(pl.CoinCount > 0, "Last layer doesn't have any coin.");

            Assert.True(p1.CoinCount + pl.CoinCount == p0.OutpointSigCount, "probabilistic filter count mismatches" +
                                                                            "deeper layers coins count.");
        }

        [Fact]
        public void AddProductionSideChainTest()
        {
            var volatileStore = new VolatilePackStore(1, new[] {4096});
            var hash = GetMockHash().Object;
            var rand = new Random(2);

            var coin1 = GetCoin(rand);
            var context = new BlockAlias(123, 0);
            var context2 = new BlockAlias(123, 1);

            var sozu = GetSozuTable(volatileStore, hash);
            var ret = sozu.AddProduction(hash.Hash(ref coin1.Outpoint), ref coin1.Outpoint, false, coin1.Payload,
                context,
                new MockLineage());
            Assert.Equal(CoinChangeStatus.Success, ret);

            // Test add production idempotence
            ret = sozu.AddProduction(hash.Hash(ref coin1.Outpoint), ref coin1.Outpoint, false, coin1.Payload,
                context, new MockLineage(()=>false));

            Assert.Equal(CoinChangeStatus.Success, ret);

            for (var i = 0; i < 3; ++i)
            {
                var coin = GetCoin(rand);
                ret = sozu.AddProduction(hash.Hash(ref coin.Outpoint), ref coin.Outpoint, false, coin.Payload, context,
                    new MockLineage());
                Assert.Equal(CoinChangeStatus.Success, ret);
            }

            // add side chain production
            ret = sozu.AddProduction(hash.Hash(ref coin1.Outpoint), ref coin1.Outpoint, false, coin1.Payload, context2,
                new MockLineage());
            Assert.Equal(CoinChangeStatus.Success, ret);

            var p0 = volatileStore.Read(0, 0);
            // assert that two add of coin1 are merged into one.
            Assert.Equal(4, p0.CoinCount);
        }

        [Fact]
        public void AddOversizedProductionTest()
        {
            var volatileStore = new VolatilePackStore(1, new[] {4096, 4096});
            var hash = GetMockHash().Object;
            var rand = new Random(2);

            var sozu = GetSozuTable(volatileStore, hash);

            var context = new BlockAlias(123, 0);
            for (var i = 0; i < 3; ++i)
            {
                var coin = GetCoin(rand);
                var ret = sozu.AddProduction(hash.Hash(ref coin.Outpoint), ref coin.Outpoint, false, coin.Payload,
                    context,
                    new MockLineage());
                Assert.Equal(CoinChangeStatus.Success, ret);
            }

            var coin2 = GetCoin(rand, SozuTable.PayloadOversizeInBytes + 10, new BlockAlias(123, 0));

            var ret2 = sozu.AddProduction(hash.Hash(ref coin2.Outpoint), ref coin2.Outpoint, false, coin2.Payload,
                context,
                new MockLineage());

            Assert.Equal(CoinChangeStatus.Success, ret2);
            var pl = volatileStore.Read(volatileStore.LayerCount -1, 0);
            Assert.True(pl.TryGet(ref coin2.Outpoint, out var coin2_p0));
            Assert.True(coin2.Span.SequenceEqual(coin2_p0.Span));
        }

        [Fact]
        public void AddConsumptionTest()
        {
            var volatileStore = new VolatilePackStore(1, new[] {4096});
            var hash = GetMockHash().Object;
            var rand = new Random(2);

            var sozu = GetSozuTable(volatileStore, hash);

            var context = new BlockAlias(123, 0);
            var coin1 = GetCoin(rand);

            var ret1 = sozu.AddProduction(hash.Hash(ref coin1.Outpoint), ref coin1.Outpoint, false, coin1.Payload,
                context,
                new MockLineage());

            Assert.Equal(CoinChangeStatus.Success, ret1);

            for (var i = 0; i < 3; ++i)
            {
                var coin = GetCoin(rand);
                var ret = sozu.AddProduction(hash.Hash(ref coin.Outpoint), ref coin.Outpoint, false, coin.Payload,
                    context,
                    new MockLineage());
                Assert.Equal(CoinChangeStatus.Success, ret);
            }

            ret1 = sozu.AddConsumption(hash.Hash(ref coin1.Outpoint), ref coin1.Outpoint, context,
                new MockLineage());

            Assert.Equal(CoinChangeStatus.Success, ret1);

            // test add consumption idempotence
            ret1 = sozu.AddConsumption(hash.Hash(ref coin1.Outpoint), ref coin1.Outpoint, context,
                new MockLineage(()=>false));

            Assert.Equal(CoinChangeStatus.Success, ret1);

            var p0 = volatileStore.Read(0, 0);
            Assert.True(p0.TryGet(ref coin1.Outpoint, out var coin1_p0));
            Assert.Equal(2, coin1_p0.Events.Length);
            Assert.True(coin1_p0.Payload.Span.SequenceEqual(coin1.Payload.Span));
        }

        [Fact]
        public void AddConsumption_OutpointNotFound_Test()
        {
            var volatileStore = new VolatilePackStore(1, new[] {4096});
            var hash = GetMockHash().Object;
            var rand = new Random(2);

            var sozu = GetSozuTable(volatileStore, hash);

            var context = new BlockAlias(123, 0);
            for (var i = 0; i < 3; ++i)
            {
                var coin = GetCoin(rand);
                var ret = sozu.AddProduction(hash.Hash(ref coin.Outpoint), ref coin.Outpoint, false, coin.Payload,
                    context,
                    new MockLineage());
                Assert.Equal(CoinChangeStatus.Success, ret);
            }

            var coin1 = GetCoin(rand);
            var ret1 = sozu.AddConsumption(hash.Hash(ref coin1.Outpoint), ref coin1.Outpoint, context,
                new MockLineage());

            Assert.Equal(CoinChangeStatus.OutpointNotFound, ret1);
        }

        [Fact]
        public void AddConsumption_InConsistentContext_Test()
        {
            var volatileStore = new VolatilePackStore(1, new[] {4096});
            var hash = GetMockHash().Object;
            var rand = new Random(2);

            var sozu = GetSozuTable(volatileStore, hash);

            var context = new BlockAlias(123, 0);
            var coin1 = GetCoin(rand);

            var ret1 = sozu.AddProduction(hash.Hash(ref coin1.Outpoint), ref coin1.Outpoint, false, coin1.Payload,
                context,
                new MockLineage());

            Assert.Equal(CoinChangeStatus.Success, ret1);

            for (var i = 0; i < 3; ++i)
            {
                var coin = GetCoin(rand);
                var ret = sozu.AddProduction(hash.Hash(ref coin.Outpoint), ref coin.Outpoint, false, coin.Payload,
                    context,
                    new MockLineage());
                Assert.Equal(CoinChangeStatus.Success, ret);
            }

            ret1 = sozu.AddConsumption(hash.Hash(ref coin1.Outpoint), ref coin1.Outpoint, context,
                new MockLineage(() => false));

            Assert.Equal(CoinChangeStatus.InvalidContext, ret1);
        }

        [Fact]
        public void RemoveProduction_Test()
        {
            var volatileStore = new VolatilePackStore(1, new[] {4096});
            var hash = GetMockHash().Object;
            var rand = new Random(2);

            var sozu = GetSozuTable(volatileStore, hash);

            var context = new BlockAlias(123, 0);
            var coin1 = GetCoin(rand);

            var ret1 = sozu.AddProduction(hash.Hash(ref coin1.Outpoint), ref coin1.Outpoint, false, coin1.Payload,
                context,
                new MockLineage());

            Assert.Equal(CoinChangeStatus.Success, ret1);

            for (var i = 0; i < 3; ++i)
            {
                var coin = GetCoin(rand);
                var ret = sozu.AddProduction(hash.Hash(ref coin.Outpoint), ref coin.Outpoint, false, coin.Payload,
                    context,
                    new MockLineage());
                Assert.Equal(CoinChangeStatus.Success, ret);
            }

            ret1 = sozu.Remove(hash.Hash(ref coin1.Outpoint), ref coin1.Outpoint, context,
                CoinRemoveOption.RemoveProduction, new MockLineage((out BlockAlias result) => result = context,
                    MockLineage.Undefined));

            Assert.Equal(CoinChangeStatus.Success, ret1);
            var p0 = volatileStore.Read(0, 0);
            Assert.True(p0.TryGet(ref coin1.Outpoint, out var coin1_p0));
            Assert.Equal(0, coin1_p0.Events.Length);
        }

        [Fact]
        public void RemoveConsumption_Test()
        {
            var volatileStore = new VolatilePackStore(1, new[] {4096});
            var hash = GetMockHash().Object;
            var rand = new Random(2);

            var sozu = GetSozuTable(volatileStore, hash);

            var context = new BlockAlias(123, 0);
            var coin1 = GetCoin(rand);

            var ret1 = sozu.AddProduction(hash.Hash(ref coin1.Outpoint), ref coin1.Outpoint, false, coin1.Payload,
                context,
                new MockLineage());

            Assert.Equal(CoinChangeStatus.Success, ret1);

            ret1 = sozu.AddConsumption(hash.Hash(ref coin1.Outpoint), ref coin1.Outpoint, context,
                new MockLineage());

            Assert.Equal(CoinChangeStatus.Success, ret1);

            for (var i = 0; i < 3; ++i)
            {
                var coin = GetCoin(rand);
                var ret = sozu.AddProduction(hash.Hash(ref coin.Outpoint), ref coin.Outpoint, false, coin.Payload,
                    context,
                    new MockLineage());
                Assert.Equal(CoinChangeStatus.Success, ret);
            }

            ret1 = sozu.Remove(hash.Hash(ref coin1.Outpoint), ref coin1.Outpoint, context,
                CoinRemoveOption.RemoveConsumption,
                new MockLineage(MockLineage.Undefined, (out BlockAlias result) => result = context));

            Assert.Equal(CoinChangeStatus.Success, ret1);
            var p0 = volatileStore.Read(0, 0);
            Assert.True(p0.TryGet(ref coin1.Outpoint, out var coin1_p0));
            Assert.Equal(1, coin1_p0.Events.Length);
            Assert.Equal(coin1_p0.Events[0], new CoinEvent(context, CoinEventKind.Production));
            Assert.True(coin1_p0.Payload.Span.SequenceEqual(coin1.Payload.Span));
        }

        [Fact]
        public void RemoveProductionAndConsumption_Test()
        {
            var volatileStore = new VolatilePackStore(1, new[] { 4096 });
            var hash = GetMockHash().Object;
            var rand = new Random(2);

            var sozu = GetSozuTable(volatileStore, hash);

            var context = new BlockAlias(123, 0);
            var coin1 = GetCoin(rand);

            var ret1 = sozu.AddProduction(hash.Hash(ref coin1.Outpoint), ref coin1.Outpoint, false, coin1.Payload,
                context,
                new MockLineage());

            Assert.Equal(CoinChangeStatus.Success, ret1);

            ret1 = sozu.AddConsumption(hash.Hash(ref coin1.Outpoint), ref coin1.Outpoint, context,
                new MockLineage());

            Assert.Equal(CoinChangeStatus.Success, ret1);

            for (var i = 0; i < 3; ++i)
            {
                var coin = GetCoin(rand);
                var ret = sozu.AddProduction(hash.Hash(ref coin.Outpoint), ref coin.Outpoint, false, coin.Payload,
                    context,
                    new MockLineage());
                Assert.Equal(CoinChangeStatus.Success, ret);
            }

            ret1 = sozu.Remove(hash.Hash(ref coin1.Outpoint), ref coin1.Outpoint, context,
                CoinRemoveOption.RemoveProduction | CoinRemoveOption.RemoveConsumption,
                new MockLineage((out BlockAlias result) => result = context, (out BlockAlias result) => result = context));

            Assert.Equal(CoinChangeStatus.Success, ret1);
            var p0 = volatileStore.Read(0, 0);
            Assert.True(p0.TryGet(ref coin1.Outpoint, out var coin1_p0));
            Assert.Equal(0, coin1_p0.Events.Length);
        }

        [Fact]
        public void TryGetCoinInFutureTest()
        {
            var volatileStore = new VolatilePackStore(1, new[] { 4096 });
            var hash = GetMockHash().Object;
            var rand = new Random(2);

            // Create a real lineage. It doesn't matter if all blocks are committed because we are just reading.
            var cBlocks = new List<CommittedBlock>();
            cBlocks.Add(new CommittedBlock(CommittedBlockId.Genesis, BlockAlias.Genesis, BlockAlias.GenesisParent));
            var firstBlockAlias = new BlockAlias(1, 0);
            cBlocks.Add(new CommittedBlock(
                CommittedBlockId.ReadFromHex("AAAAAAAABBBBBBBBCCCCCCCCDDDDDDDDAAAAAAAABBBBBBBBCCCCCCCCDDDDDDDD"), 
                firstBlockAlias, BlockAlias.Genesis));
            var futureBlockAlias = new BlockAlias(2, 0);
            cBlocks.Add(new CommittedBlock(
                CommittedBlockId.ReadFromHex("AAAABBBBBBBBCCCCCCCCDDDDDDDDAAAAAAAABBBBBBBBCCCCCCCCDDDDDDDDAAAA"), 
                futureBlockAlias, firstBlockAlias));

            var realLineage = new Lineage(cBlocks, new List<UncommittedBlock>(), 100);

            var sozu = GetSozuTable(volatileStore, hash);

            var coin1 = GetCoin(rand);

            var ret = sozu.AddProduction(hash.Hash(ref coin1.Outpoint), ref coin1.Outpoint, false, coin1.Payload,
                futureBlockAlias,
                realLineage);
            Assert.Equal(CoinChangeStatus.Success, ret);

            var read = sozu.TryGet(hash.Hash(ref coin1.Outpoint), ref coin1.Outpoint, firstBlockAlias,
                realLineage, out var coin, out var prod, out var cons);
            Assert.False(read);
        }

        [Fact]
        public void TryAddConsumptionInPastTest()
        {
            var volatileStore = new VolatilePackStore(1, new[] { 4096 });
            var hash = GetMockHash().Object;
            var rand = new Random(2);

            // Create a real lineage
            var cBlocks = new List<CommittedBlock>();
            cBlocks.Add(new CommittedBlock(CommittedBlockId.Genesis, BlockAlias.Genesis, BlockAlias.GenesisParent));
            var firstBlockAlias = new BlockAlias(1, 0);
            cBlocks.Add(new CommittedBlock(
                CommittedBlockId.ReadFromHex("AAAAAAAABBBBBBBBCCCCCCCCDDDDDDDDAAAAAAAABBBBBBBBCCCCCCCCDDDDDDDD"),
                firstBlockAlias, BlockAlias.Genesis));
            var futureBlockAlias = new BlockAlias(2, 0);
            cBlocks.Add(new CommittedBlock(
                CommittedBlockId.ReadFromHex("AAAABBBBBBBBCCCCCCCCDDDDDDDDAAAAAAAABBBBBBBBCCCCCCCCDDDDDDDDAAAA"),
                futureBlockAlias, firstBlockAlias));

            var realLineage = new Lineage(cBlocks, new List<UncommittedBlock>(), 100);

            var sozu = GetSozuTable(volatileStore, hash);

            var coin1 = GetCoin(rand);

            // The next two commands will work because the fact that a block is uncommitted is not verified anymore.
            // If this test is moved into the ICoinStore at any point, adding a previous consumption should fail
            // already because a 'previous' block always has to be committed.
            var ret = sozu.AddProduction(hash.Hash(ref coin1.Outpoint), ref coin1.Outpoint, false, coin1.Payload,
                futureBlockAlias,
                realLineage);
            Assert.Equal(CoinChangeStatus.Success, ret);

            var cons = sozu.AddConsumption(hash.Hash(ref coin1.Outpoint), ref coin1.Outpoint, firstBlockAlias,
                realLineage);

            Assert.Equal(CoinChangeStatus.InvalidContext, cons);
        }

        [Fact]
        public void TryAddConsumptionInSideChainTest()
        {
            var volatileStore = new VolatilePackStore(1, new[] { 4096 });
            var hash = GetMockHash().Object;
            var rand = new Random(2);

            // Create a real lineage
            var cBlocks = new List<CommittedBlock>();
            cBlocks.Add(new CommittedBlock(CommittedBlockId.Genesis, BlockAlias.Genesis, BlockAlias.GenesisParent));

            var uncBlocks = new List<UncommittedBlock>();

            var firstBlockAlias = new BlockAlias(1, 0);
            var uncId = new byte[16];
            rand.NextBytes(uncId);

            uncBlocks.Add(new UncommittedBlock(
                UncommittedBlockId.ReadFrom(uncId),
                firstBlockAlias, BlockAlias.Genesis));

            var parallelBlockAlias = new BlockAlias(1, 1);
            rand.NextBytes(uncId);

            uncBlocks.Add(new UncommittedBlock(
                UncommittedBlockId.ReadFrom(uncId),
                parallelBlockAlias, BlockAlias.Genesis));

            var realLineage = new Lineage(cBlocks, uncBlocks, 100);

            var sozu = GetSozuTable(volatileStore, hash);

            var coin1 = GetCoin(rand);

            var ret = sozu.AddProduction(hash.Hash(ref coin1.Outpoint), ref coin1.Outpoint, false, coin1.Payload,
                firstBlockAlias,
                realLineage);
            Assert.Equal(CoinChangeStatus.Success, ret);

            var cons = sozu.AddConsumption(hash.Hash(ref coin1.Outpoint), ref coin1.Outpoint, parallelBlockAlias,
                realLineage);

            Assert.Equal(CoinChangeStatus.InvalidContext, cons);
        }
    }
}
