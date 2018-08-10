using System;
using Terab.Server;
using Terab.UTXO.Core.Blockchain;
using Terab.UTXO.Core.Hash;
using Terab.UTXO.Core.Messaging;
using Terab.UTXO.Core.Request;
using Terab.UTXO.Core.Response;
using Xunit;

namespace Terab.UTXO.Core.Tests
{
    public class ControllerThreadTests
    {
        private readonly SimpleBlockchain _chain = new SimpleBlockchain();
        private BoundedInbox _inbox;
        private BoundedInbox _outbox;
        private ControllerThread _controller;

        private BlockAlias _1 = new BlockAlias(1);
        private BlockAlias _2 = new BlockAlias(2);
        private BlockAlias _3 = new BlockAlias(3);
        private BlockAlias _4 = new BlockAlias(4);
        private BlockAlias _5 = new BlockAlias(5);
        private BlockAlias _9 = new BlockAlias(9);

        public void Setup()
        {
            var id = _chain.OpenFirstBlock().Alias;
            _chain.CommitBlock(id, BlockId.Genesis);

            id = _chain.OpenBlock(_1).Alias;
            _chain.CommitBlock(id, new BlockId(new Hash256(0x11111111UL, 0x22222222UL, 0x33333333UL, 0x44444444UL)));
            id = _chain.OpenBlock(_2).Alias;
            _chain.CommitBlock(id, new BlockId(new Hash256(0xFFFFFFFFUL, 0xEEEEEEEEUL, 0xDDDDDDDDUL, 0xCCCCCCCCUL)));
            // Second child for block 2
            id = _chain.OpenBlock(_2).Alias;
            _chain.CommitBlock(id,
                new BlockId(new Hash256(0x1111111122UL, 0x2222222233UL, 0x3333333344UL, 0x4444444455UL)));
            // main chain prolonged
            id = _chain.OpenBlock(_3).Alias;
            _chain.CommitBlock(id,
                new BlockId(new Hash256(0x1111111122UL, 0x2222222233UL, 0x3333333344UL, 0x4444444455UL)));
            // side chain prolonged
            id = _chain.OpenBlock(_4).Alias;
            _chain.CommitBlock(id,
                new BlockId(new Hash256(0x1111111122UL, 0x2222222233UL, 0x3333333344UL, 0x4444444455UL)));
            // fork on end of main chain
            id = _chain.OpenBlock(_5).Alias;
            _chain.CommitBlock(id,
                new BlockId(new Hash256(0x1111111122UL, 0x2222222233UL, 0x3333333344UL, 0x4444444455UL)));
            id = _chain.OpenBlock(_5).Alias;
            _chain.CommitBlock(id,
                new BlockId(new Hash256(0x1111111122UL, 0x2222222233UL, 0x3333333344UL, 0x4444444455UL)));

            var chainStrategy = new BlockchainStrategies();
            var opti = new OptimizedLineage(chainStrategy.GetQuasiOrphans(_chain), _2);

            _controller = new ControllerThread(_chain, opti, _inbox = BoundedInbox.Create(),
                _outbox = BoundedInbox.Create());
        }

        [Fact]
        public void TestIsAncestor()
        {
            Setup();

            var messageAllocation = new Span<byte>(new byte[IsAncestor.SizeInBytes]);

            var isAncestorReq = new IsAncestor(1, 5, _3, _1);
            // Serialize message to put it into the inbox
            MessageSerializers.ClientSerializeIsAncestor(isAncestorReq, messageAllocation);

            _inbox.TryWrite(messageAllocation);

            _controller.DoWork();

            var result = _outbox.Peek();

            Assert.Equal(MessageType.AncestorResponse, ClientServerMessage.GetMessageType(result));
            Assert.True(ClientServerMessage.TryGetLength(result, out int responseSize));
            Assert.Equal(AncestorResponse.SizeInBytes, responseSize);

            var response = MessageSerializers.ClientDeserializeAncestorResponse(result);

            Assert.Equal(1U, response.RequestId);
            Assert.Equal(5U, response.ClientId);
            Assert.True(response.Answer);

            // get a wrong ancestor
            isAncestorReq = new IsAncestor(1, 5, _3, _5);
            // Serialize message to put it into the inbox
            MessageSerializers.ClientSerializeIsAncestor(isAncestorReq, messageAllocation);

            _inbox.TryWrite(messageAllocation);

            _controller.DoWork();

            _outbox.Next();
            result = _outbox.Peek();
            var response2 = MessageSerializers.ClientDeserializeAncestorResponse(result);
            Assert.Equal(1U, response2.RequestId);
            Assert.Equal(5U, response2.ClientId);
            Assert.Equal(MessageType.AncestorResponse, response2.ResponseType);

            Assert.False(response2.Answer);
        }

        [Fact]
        public void TestIsPruneable()
        {
            Setup();

            var messageAllocation = new Span<byte>(new byte[IsPruneable.SizeInBytes]);

            var isPruneableReq = new IsPruneable(1, 5, _3);
            // Serialize message to put it into the inbox
            MessageSerializers.ClientSerializeIsPruneable(isPruneableReq, messageAllocation);

            _inbox.TryWrite(messageAllocation);

            _controller.DoWork();

            var result = _outbox.Peek();
            var response = MessageSerializers.ClientDeserializePruneableResponse(result);

            Assert.Equal(MessageType.PruneableResponse, ClientServerMessage.GetMessageType(result));

            Assert.Equal(1U, response.RequestId);
            Assert.Equal(5U, response.ClientId);
            Assert.False(response.Answer);

            _outbox.Next();

            isPruneableReq = new IsPruneable(1, 5, _4);
            // Serialize message to put it into the inbox
            MessageSerializers.ClientSerializeIsPruneable(isPruneableReq, messageAllocation);

            _inbox.TryWrite(messageAllocation);

            _controller.DoWork();

            result = _outbox.Peek();
            Assert.Equal(MessageType.PruneableResponse, ClientServerMessage.GetMessageType(result));
            var response2 = MessageSerializers.ClientDeserializePruneableResponse(result);

            Assert.Equal(1U, response.RequestId);
            Assert.Equal(5U, response.ClientId);
            Assert.True(response2.Answer);
        }

        [Fact]
        public TempBlockId TestOpenBlock()
        {
            Setup();

            var messageAllocation = new Span<byte>(new byte[OpenBlock.SizeInBytes]);

            var openBlock = new OpenBlock(1, 5, _3);
            // Serialize message to put it into the inbox
            MessageSerializers.ClientSerializeOpenBlock(openBlock, messageAllocation);

            _inbox.TryWrite(messageAllocation);

            _controller.DoWork();

            var result = _outbox.Peek();
            Assert.Equal(MessageType.OpenedBlock, ClientServerMessage.GetMessageType(result));
            var response = MessageSerializers.ClientDeserializeOpenedBlock(result);
            Assert.Equal(1U, response.RequestId);
            Assert.Equal(5U, response.ClientId);
            Assert.Equal(_9, response.Alias);

            return response.UncommittedBlockId;
        }

        [Fact]
        public void TestOpenFirstBlock()
        {
            _controller = new ControllerThread(_chain, default(OptimizedLineage), _inbox = BoundedInbox.Create(),
                _outbox = BoundedInbox.Create());

            var messageAllocation = new Span<byte>(new byte[OpenBlock.SizeInBytes]);

            var openBlock = new OpenBlock(1, 5, BlockAlias.GenesisParent);
            // Serialize message to put it into the inbox
            MessageSerializers.ClientSerializeOpenBlock(openBlock, messageAllocation);

            _inbox.TryWrite(messageAllocation);

            _controller.DoWork();

            var result = _outbox.Peek();

            Assert.Equal(MessageType.OpenedBlock, ClientServerMessage.GetMessageType(result));
            var response = MessageSerializers.ClientDeserializeOpenedBlock(result);

            Assert.Equal(1U, response.RequestId);
            Assert.Equal(5U, response.ClientId);
            Assert.Equal(_1, response.Alias);

            var tmpId = response.UncommittedBlockId;
            Console.WriteLine(tmpId);
        }

        [Fact]
        public void TestGetUncommittedBlockHandle()
        {
            var uncommitedBlockId = TestOpenBlock();

            var messageAllocation = new Span<byte>(new byte[GetUncommittedBlockHandle.SizeInBytes]);

            var getHandle = new GetUncommittedBlockHandle(1, 5, uncommitedBlockId);
            // Serialize message to put it into the inbox
            MessageSerializers.ClientSerializeGetUncommittedBlockHandle(getHandle, messageAllocation);

            _inbox.TryWrite(messageAllocation);

            _controller.DoWork();

            _outbox.Next(); // because we did the TestOpenBlock in the beginning
            var result = _outbox.Peek();

            Assert.Equal(MessageType.BlockHandle, ClientServerMessage.GetMessageType(result));
            var response = MessageSerializers.ClientDeserializeBlockHandleResponse(result);

            Assert.Equal(1U, response.RequestId);
            Assert.Equal(5U, response.ClientId);
            Assert.Equal(_9, response.BlockHandle);
        }

        [Fact]
        public void TestGetCommittedBlockHandle()
        {
            Setup();

            var messageAllocation = new Span<byte>(new byte[GetBlockHandle.SizeInBytes]);

            var getHandle =
                new GetBlockHandle(1, 5,
                    new BlockId(new Hash256(0xFFFFFFFFUL, 0xEEEEEEEEUL, 0xDDDDDDDDUL, 0xCCCCCCCCUL)));
            // Serialize message to put it into the inbox
            MessageSerializers.ClientSerializeGetBlockHandle(getHandle, messageAllocation);

            _inbox.TryWrite(messageAllocation);

            _controller.DoWork();

            var result = _outbox.Peek();

            Assert.Equal(MessageType.BlockHandle, ClientServerMessage.GetMessageType(result));
            var response = MessageSerializers.ClientDeserializeBlockHandleResponse(result);

            Assert.Equal(1U, response.RequestId);
            Assert.Equal(5U, response.ClientId);

            Assert.Equal(_3, response.BlockHandle);
        }

        [Fact]
        public void TestCommitBlock()
        {
            TestOpenBlock();

            var messageAllocation = new Span<byte>(new byte[CommitBlock.SizeInBytes]);

            var commitBlock = new CommitBlock(1, 5, _9,
                new BlockId(new Hash256(0xFFFFFFFFUL, 0xEEEEEEEEUL, 0xDDDDDDDDUL, 0xCCCCCCCEUL)));
            // Serialize message to put it into the inbox

            MessageSerializers.ClientSerializeCommitBlock(commitBlock, messageAllocation);

            _inbox.TryWrite(messageAllocation);

            _controller.DoWork();

            _outbox.Next();
            var result = _outbox.Peek();

            Assert.Equal(MessageType.EverythingOk, ClientServerMessage.GetMessageType(result));
            var response = MessageSerializers.ClientDeserializeEverythingOk(result);

            Assert.Equal(1U, response.RequestId);
            Assert.Equal(5U, response.ClientId);
        }

        [Fact]
        public void TestGetCommittedBlockInfo()
        {
            Setup();

            var messageAllocation = new Span<byte>(new byte[GetBlockInformation.SizeInBytes]);

            var getInfo = new GetBlockInformation(1, 5, _1);
            // Serialize message to put it into the inbox
            MessageSerializers.ClientSerializeGetBlockInformation(getInfo, messageAllocation);

            _inbox.TryWrite(messageAllocation);

            _controller.DoWork();

            var result = _outbox.Peek();

            Assert.Equal(MessageType.CommittedBlockInfo, ClientServerMessage.GetMessageType(result));
            var response = MessageSerializers.ClientDeserializeCommittedBlockInfo(result);

            Assert.Equal(1U, response.RequestId);
            Assert.Equal(5U, response.ClientId);

            Assert.Equal(BlockId.Genesis, response.BlockId);
            Assert.Equal(_1, response.Alias);
            Assert.Equal(0u, response.Parent.Value);
            Assert.Equal(0, response.BlockHeight);
        }

        [Fact]
        public void TestGetUncommittedBlockInfo()
        {
            var uncommittedBlockId = TestOpenBlock();

            var messageAllocation = new Span<byte>(new byte[GetBlockInformation.SizeInBytes]);

            var getInfo = new GetBlockInformation(1, 5, _9);
            // Serialize message to put it into the inbox

            MessageSerializers.ClientSerializeGetBlockInformation(getInfo, messageAllocation);

            _inbox.TryWrite(messageAllocation);

            _controller.DoWork();

            _outbox.Next();
            var result = _outbox.Peek();

            Assert.Equal(MessageType.UncommittedBlockInfo, ClientServerMessage.GetMessageType(result));
            var response = MessageSerializers.ClientDeserializeUncommittedBlockInfo(result);

            Assert.Equal(1U, response.RequestId);
            Assert.Equal(5U, response.ClientId);

            Assert.Equal(uncommittedBlockId, response.UncommittedBlockId);
            Assert.Equal(_9, response.Alias);
            Assert.Equal(_3, response.Parent);
            Assert.Equal(3, response.BlockHeight);
        }
    }
}