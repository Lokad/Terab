// Copyright Lokad 2018 under MIT BCH.
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using Moq;
using Terab.Lib.Chains;
using Terab.Lib.Coins;
using Terab.Lib.Messaging;
using Terab.Lib.Messaging.Protocol;
using Terab.Lib.Tests.Mock;
using Xunit;

namespace Terab.Lib.Tests
{
    public unsafe class CoinControllerTests
    {
        private static Coin GetCoin(Random rand)
        {
            return GetCoin(rand, (byte) rand.Next());
        }

        private static Coin GetCoin(Random rand, int scriptLength)
        {
            var coin = new Coin(new byte[4096]);

            var outpoint = new Outpoint();
            for (var i = 0; i < 32; i++)
                outpoint.TxId[i] = (byte) rand.Next();
            outpoint.TxIndex = rand.Next();

            coin.Outpoint = outpoint;

            var events = new[] {new CoinEvent(new BlockAlias(123, 0), CoinEventKind.Production)};
            coin.SetEvents(events);

            var script = new byte[scriptLength];
            rand.NextBytes(script);

            var payload = new Payload(new byte[4096]);
            payload.NLockTime = (uint) rand.Next();
            payload.Satoshis = (ulong) rand.Next();
            payload.Append(script);

            coin.SetPayload(payload);

            return coin;
        }

        private IOutpointHash _hash;

        private Random _rand;

        public CoinControllerTests()
        {
            var mockHash = new Mock<IOutpointHash>();
            mockHash.Setup(x => x.Hash(ref It.Ref<Outpoint>.IsAny))
                .Returns((Outpoint p) => BinaryPrimitives.ReadUInt64BigEndian(new ReadOnlySpan<byte>(p.TxId, 8)));

            _hash = mockHash.Object;
            _rand = new Random(2);
        }

        [Fact]
        public void ReadExistingCoin()
        {
            var sozu = new VolatileCoinStore();

            var inbox = new BoundedInbox();
            var outbox = new BoundedInbox();
            var controller = new CoinController(inbox, outbox, sozu, _hash);

            var coin = GetCoin(_rand);

            sozu.AddProduction(
                _hash.Hash(ref coin.Outpoint),
                ref coin.Outpoint,
                false, coin.Payload,
                new BlockAlias(3),
                null); // lineage is not used in VolatileCoinStore

            var clientId = new ClientId();
            var reqId = new RequestId(1);

            var context = new BlockAlias(3);

            var readCoinRequest = GetCoinRequest.From(reqId, clientId, coin.Outpoint, context, clientId.Mask);

            inbox.TryWrite(readCoinRequest.Span);
            controller.HandleRequest();

            var raw = outbox.Peek();
            var response = new GetCoinResponse(raw.Span);
            Assert.Equal(response.MessageHeader.MessageSizeInBytes, raw.Length);

            // verify response contents
            Assert.Equal(reqId, response.MessageHeader.RequestId);
            Assert.Equal(clientId, response.MessageHeader.ClientId);
            Assert.Equal(MessageKind.GetCoinResponse, response.MessageHeader.MessageKind);
            Assert.Equal(coin.Outpoint, response.Outpoint);
            Assert.Equal(OutpointFlags.None, response.OutpointFlags);
            Assert.Equal(context, response.Context.ConvertToBlockAlias(clientId.Mask));
            Assert.Equal(context, response.Production.ConvertToBlockAlias(clientId.Mask));
            Assert.Equal(BlockAlias.Undefined.ConvertToBlockHandle(clientId.Mask), response.Consumption);
            Assert.Equal(coin.Payload.Satoshis, response.Satoshis);
            Assert.Equal(coin.Payload.NLockTime, response.NLockTime);
            Assert.True(coin.Payload.Script.SequenceEqual(response.Script));
        }

        private ILineage MakeLineage()
        {
            var blockId1 = CommittedBlockId.ReadFromHex("0000000000000000000000000000000000000000000000000000000000AAA333");
            var commBlock1 = new CommittedBlock(blockId1, BlockAlias.Genesis, BlockAlias.GenesisParent);
            var blockId2 = CommittedBlockId.ReadFromHex("0000000000000000000000000000000000000000000000000000000000BBB444");
            var commBlock2 = new CommittedBlock(blockId2, new BlockAlias(2), BlockAlias.Genesis);
            var blockId3 = CommittedBlockId.ReadFromHex("0000000000000000000000000000000000000000000000000000000000CCC555");
            var commBlock3 = new CommittedBlock(blockId3, new BlockAlias(3), new BlockAlias(2));

            var uncommBlock = new UncommittedBlock(UncommittedBlockId.Create(), new BlockAlias(4), new BlockAlias(3));

            var committedBlocks = new List<CommittedBlock>();
            committedBlocks.Add(commBlock1);
            committedBlocks.Add(commBlock2);
            committedBlocks.Add(commBlock3);

            var uncommittedBlocks = new List<UncommittedBlock>();
            uncommittedBlocks.Add(uncommBlock);

            return new Lineage(committedBlocks, uncommittedBlocks, 100);
        }

        [Fact]
        public void TryReadingNonExistentCoin()
        {
            var sozu = new VolatileCoinStore();

            var inbox = new BoundedInbox();
            var outbox = new BoundedInbox();
            var controller = new CoinController(inbox, outbox, sozu, _hash);

            var clientId = new ClientId();
            var reqId = new RequestId(1);

            var coin = GetCoin(_rand);

            var readCoinRequest = GetCoinRequest.From(reqId, clientId, coin.Outpoint,
                new BlockAlias(3), clientId.Mask);

            inbox.TryWrite(readCoinRequest.Span);
            controller.HandleRequest();

            var raw = outbox.Peek();
            var response = new GetCoinResponse(raw.Span);

            // verify response contents
            Assert.Equal(reqId, response.MessageHeader.RequestId);
            Assert.Equal(clientId, response.MessageHeader.ClientId);
            Assert.Equal(GetCoinStatus.OutpointNotFound, response.Status);
        }

        /// <summary>
        /// As this test is a CoinControllerUnitTest and no integration test,
        /// it is not verified that in the Sozu table is written what is supposed
        /// to be written. It just verifies that the Controller processes the
        /// request and replies with an appropriate response.
        /// </summary>
        [Fact]
        public void WriteCoinProd()
        {
            var sozu = new VolatileCoinStore();

            var inbox = new BoundedInbox();
            var outbox = new BoundedInbox();
            var controller = new CoinController(inbox, outbox, sozu, _hash);
            controller.Lineage = new MockLineage();

            var clientId = new ClientId();
            var reqId = new RequestId(1);

            var coin = GetCoin(_rand);

            var writeCoinRequest = ProduceCoinRequest.From(
                reqId,
                clientId,
                coin.Outpoint,
                (OutpointFlags) coin.OutpointFlags,
                new BlockHandle(2).ConvertToBlockAlias(clientId.Mask),
                100, 50,
                coin.Payload.Script,
                clientId.Mask);

            inbox.TryWrite(writeCoinRequest.Span);
            controller.HandleRequest();

            var raw = outbox.Peek();
            Assert.Equal(ProduceCoinResponse.SizeInBytes, raw.Length);
            var response = new ProduceCoinResponse(raw.Span);
            // verify response contents
            Assert.Equal(reqId, response.MessageHeader.RequestId);
            Assert.Equal(clientId, response.MessageHeader.ClientId);
            Assert.Equal(MessageKind.ProduceCoinResponse, response.MessageHeader.MessageKind);
            Assert.Equal(ChangeCoinStatus.Success, response.Status);
        }

        /// <summary>
        /// As this test is a CoinControllerUnitTest and no integration test,
        /// it is not verified that in the Sozu table is written what is supposed
        /// to be written. It just verifies that the Controller processes the
        /// request and replies with an appropriate response.
        /// </summary>
        [Fact]
        public void WriteCoinCons()
        {
            var sozu = new VolatileCoinStore();

            var inbox = new BoundedInbox();
            var outbox = new BoundedInbox();
            var controller = new CoinController(inbox, outbox, sozu, _hash);
            controller.Lineage = new MockLineage();

            var clientId = new ClientId();
            var reqId = new RequestId(1);

            var coin = GetCoin(_rand);

            sozu.AddProduction(
                _hash.Hash(ref coin.Outpoint),
                ref coin.Outpoint,
                false, coin.Payload,
                new BlockAlias(2),
                null);

            var pool = new SpanPool<byte>(4096);
            var buffer = new byte[ConsumeCoinRequest.SizeInBytes];
            var writeCoinRequest = new ConsumeCoinRequest(
                reqId, ref coin.Outpoint, new BlockAlias(2).ConvertToBlockHandle(clientId.Mask), pool);

            inbox.TryWrite(writeCoinRequest.Span);
            controller.HandleRequest();

            var raw = outbox.Peek();
            Assert.Equal(ConsumeCoinResponse.SizeInBytes, raw.Length);
            var response = new ConsumeCoinResponse(raw.Span);
            // verify response contents
            Assert.Equal(reqId, response.MessageHeader.RequestId);
            Assert.Equal(clientId, response.MessageHeader.ClientId);
            Assert.Equal(MessageKind.ConsumeCoinResponse, response.MessageHeader.MessageKind);
            Assert.Equal(ChangeCoinStatus.Success, response.Status);
        }

        [Fact]
        public void TryWriteCoinIntoCommittedBlock()
        {
            var sozu = new VolatileCoinStore();

            var inbox = new BoundedInbox();
            var outbox = new BoundedInbox();
            var controller = new CoinController(inbox, outbox, sozu, _hash);
            controller.Lineage = MakeLineage();

            var clientId = new ClientId();
            var reqId = new RequestId(1);

            var coin = GetCoin(_rand);

            var writeCoinRequest = ProduceCoinRequest.From(
                reqId,
                clientId,
                coin.Outpoint,
                (OutpointFlags) coin.OutpointFlags,
                new BlockAlias(3),
                100, 50,
                coin.Payload.Script,
                clientId.Mask);

            inbox.TryWrite(writeCoinRequest.Span);
            controller.HandleRequest();

            var raw = outbox.Peek();
            Assert.Equal(ProduceCoinResponse.SizeInBytes, raw.Length);
            var response = new ProduceCoinResponse(raw.Span);
            // verify response contents
            Assert.Equal(reqId, response.MessageHeader.RequestId);
            Assert.Equal(clientId, response.MessageHeader.ClientId);
            Assert.Equal(MessageKind.ProduceCoinResponse, response.MessageHeader.MessageKind);
            Assert.Equal(ChangeCoinStatus.InvalidContext, response.Status);
        }

        [Fact]
        public void TryWriteCoinEmptyPayload()
        {
            var sozu = new VolatileCoinStore();

            var inbox = new BoundedInbox();
            var outbox = new BoundedInbox();
            var controller = new CoinController(inbox, outbox, sozu, _hash);
            controller.Lineage = MakeLineage();

            var clientId = new ClientId();
            var reqId = new RequestId(1);

            var coin = GetCoin(_rand);

            var writeCoinRequest = ProduceCoinRequest.From(reqId, clientId, coin.Outpoint,
                (OutpointFlags) coin.OutpointFlags,
                new BlockAlias(3), 100, 50, Span<byte>.Empty, clientId.Mask);

            inbox.TryWrite(writeCoinRequest.Span);
            controller.HandleRequest();

            var raw = outbox.Peek();
            Assert.Equal(ProduceCoinResponse.SizeInBytes, raw.Length);
            var response = new ProduceCoinResponse(raw.Span);
            // verify response contents
            Assert.Equal(reqId, response.MessageHeader.RequestId);
            Assert.Equal(clientId, response.MessageHeader.ClientId);
            Assert.Equal(MessageKind.ProduceCoinResponse, response.MessageHeader.MessageKind);
            Assert.Equal(ChangeCoinStatus.InvalidContext, response.Status);
        }

        [Fact]
        public void RemoveProduction()
        {
            var sozu = new VolatileCoinStore();

            var inbox = new BoundedInbox();
            var outbox = new BoundedInbox();
            var controller = new CoinController(inbox, outbox, sozu, _hash);
            controller.Lineage = new MockLineage();

            var clientId = new ClientId();
            var reqId = new RequestId(1);

            var coin = GetCoin(_rand);
            sozu.AddProduction(
                _hash.Hash(ref coin.Outpoint),
                ref coin.Outpoint,
                false, coin.Payload,
                new BlockAlias(2),
                null);

            var buffer = new byte[RemoveCoinRequest.SizeInBytes];
            var removeCoinRequest = new RemoveCoinRequest(
                buffer, reqId, ref coin.Outpoint, new BlockAlias(2).ConvertToBlockHandle(clientId.Mask), true, false);

            inbox.TryWrite(removeCoinRequest.Span);
            controller.HandleRequest();

            var raw = outbox.Peek();
            Assert.Equal(RemoveCoinResponse.SizeInBytes, raw.Length);
            var response = new RemoveCoinResponse(raw.Span);
            // verify response contents
            Assert.Equal(reqId, response.MessageHeader.RequestId);
            Assert.Equal(clientId, response.MessageHeader.ClientId);
            Assert.Equal(MessageKind.RemoveCoinResponse, response.MessageHeader.MessageKind);
            Assert.Equal(ChangeCoinStatus.Success, response.Status);

            sozu.TryGet(_hash.Hash(ref coin.Outpoint), ref coin.Outpoint, new BlockAlias(2), null, out var coin2,
                out var pe, out var ce);

            Assert.False(pe.IsDefined);
            Assert.False(ce.IsDefined);
        }

        [Fact]
        public void RemoveProductionAndConsumption()
        {
            var sozu = new VolatileCoinStore();

            var inbox = new BoundedInbox();
            var outbox = new BoundedInbox();
            var controller = new CoinController(inbox, outbox, sozu, _hash);
            controller.Lineage = new MockLineage();

            var clientId = new ClientId();
            var reqId = new RequestId(1);

            var coin = GetCoin(_rand);
            sozu.AddProduction(
                _hash.Hash(ref coin.Outpoint),
                ref coin.Outpoint,
                false, coin.Payload,
                new BlockAlias(2),
                null);

            sozu.AddConsumption(
                _hash.Hash(ref coin.Outpoint),
                ref coin.Outpoint,
                new BlockAlias(2),
                null);

            var buffer = new byte[RemoveCoinRequest.SizeInBytes];
            var removeCoinRequest = new RemoveCoinRequest(
                buffer, reqId, ref coin.Outpoint, new BlockAlias(2).ConvertToBlockHandle(clientId.Mask), true, true);

            inbox.TryWrite(removeCoinRequest.Span);
            controller.HandleRequest();

            var raw = outbox.Peek();
            Assert.Equal(RemoveCoinResponse.SizeInBytes, raw.Length);
            var response = new RemoveCoinResponse(raw.Span);
            // verify response contents
            Assert.Equal(reqId, response.MessageHeader.RequestId);
            Assert.Equal(clientId, response.MessageHeader.ClientId);
            Assert.Equal(MessageKind.RemoveCoinResponse, response.MessageHeader.MessageKind);
            Assert.Equal(ChangeCoinStatus.Success, response.Status);

            sozu.TryGet(_hash.Hash(ref coin.Outpoint), ref coin.Outpoint, new BlockAlias(2), null, out var coin2,
                out var pe, out var ce);

            Assert.False(pe.IsDefined);
            Assert.False(ce.IsDefined);
        }
    }
}