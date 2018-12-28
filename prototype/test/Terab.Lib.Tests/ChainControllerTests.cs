// Copyright Lokad 2018 under MIT BCH.
using Terab.Lib.Chains;
using Terab.Lib.Messaging;
using Terab.Lib.Messaging.Protocol;
using Terab.Lib.Tests.Mock;
using Xunit;
using CommitBlockStatus = Terab.Lib.Chains.CommitBlockStatus;
using OpenBlockStatus = Terab.Lib.Chains.OpenBlockStatus;

namespace Terab.Lib.Tests
{
    public class ChainControllerTests
    {
        private readonly BlockAlias _1 = new BlockAlias(1, 0);
        private readonly BlockAlias _2 = new BlockAlias(2, 0);
        private readonly BlockAlias _2_1 = new BlockAlias(2, 1);
        private readonly BlockAlias _3 = new BlockAlias(3, 0);
        private readonly BlockAlias _3_1 = new BlockAlias(3, 1);
        private readonly BlockAlias _5 = new BlockAlias(5, 0);
        private readonly BlockAlias _4_0 = new BlockAlias(4, 0);
        private readonly BlockAlias _4_1 = new BlockAlias(4, 1);
        private readonly BlockAlias _4_2 = new BlockAlias(4, 2);

        private readonly CommittedBlockId _id_1 = GetMockBlockId(1);
        private readonly CommittedBlockId _id_2 = GetMockBlockId(2);
        private readonly CommittedBlockId _id_2_1 = GetMockBlockId(3);
        private readonly CommittedBlockId _id_3 = GetMockBlockId(4);
        private readonly CommittedBlockId _id_3_1 = GetMockBlockId(5);
        private readonly CommittedBlockId _id_4_0 = GetMockBlockId(6);
        private readonly CommittedBlockId _id_4_1 = GetMockBlockId(7);

        private readonly RequestId _r1 = new RequestId(1);

        private static unsafe CommittedBlockId GetMockBlockId(byte filler)
        {
            var blockId = new CommittedBlockId();
            for (var i = 0; i < CommittedBlockId.SizeInBytes; i++)
                blockId.Data[i] = filler;

            return blockId;
        }

        public (IChainStore, BoundedInbox, BoundedInbox, ChainController, ClientId) Setup(int pruneLimit = 100)
        {
            var store = new VolatileChainStore();

            store.BlockPruneLimitDistance = pruneLimit;

            Assert.Equal(OpenBlockStatus.Success,
                store.TryOpenBlock(CommittedBlockId.GenesisParent, out var openedBlock));
            var id = openedBlock.Alias;
            Assert.NotEqual(BlockAlias.GenesisParent, id);

            Assert.Equal(CommitBlockStatus.Success, store.TryCommitBlock(id, CommittedBlockId.Genesis, out _));
            // C(0)

            Assert.Equal(OpenBlockStatus.Success, store.TryOpenBlock(CommittedBlockId.Genesis, out var cb1));
            Assert.Equal(_1, cb1.Alias);
            Assert.Equal(CommitBlockStatus.Success, store.TryCommitBlock(cb1.Alias, _id_1, out _));
            // C(0) -> C(1)

            Assert.Equal(OpenBlockStatus.Success, store.TryOpenBlock(_id_1, out var cb2));
            Assert.Equal(_2, cb2.Alias);
            Assert.Equal(CommitBlockStatus.Success, store.TryCommitBlock(cb2.Alias, _id_2, out _));
            // C(0) -> C(1) -> C(2)

            // Second child for block 2
            Assert.Equal(OpenBlockStatus.Success, store.TryOpenBlock(_id_1, out var cb3));
            Assert.Equal(_2_1, cb3.Alias);
            Assert.Equal(CommitBlockStatus.Success, store.TryCommitBlock(cb3.Alias, _id_2_1, out _));
            // C(0) -> C(1) -> C(2)
            //             \-> C(2-1)

            // main chain prolonged
            Assert.Equal(OpenBlockStatus.Success, store.TryOpenBlock(_id_2, out var cb4));
            Assert.Equal(_3, cb4.Alias);
            Assert.Equal(CommitBlockStatus.Success, store.TryCommitBlock(cb4.Alias, _id_3, out _));
            // C(0) -> C(1) -> C(2)  -> C(3)
            //             \-> C(2-1)

            // side chain prolonged
            Assert.Equal(OpenBlockStatus.Success, store.TryOpenBlock(_id_2_1, out var cb5));
            // the new assert
            Assert.Equal(_3_1, cb5.Alias);
            // the new assert
            Assert.Equal(CommitBlockStatus.Success, store.TryCommitBlock(cb5.Alias, _id_3_1, out _));
            // C(0) -> C(1) -> C(2)   -> C(3)
            //             \-> C(2-1) -> C(3-1)

            // fork on end of main chain
            Assert.Equal(OpenBlockStatus.Success, store.TryOpenBlock(_id_3, out var cb6));
            Assert.Equal(_4_0, cb6.Alias);
            Assert.Equal(CommitBlockStatus.Success, store.TryCommitBlock(cb6.Alias, _id_4_0, out _));
            
            Assert.Equal(OpenBlockStatus.Success, store.TryOpenBlock(_id_3, out var cb7));
            Assert.Equal(_4_1, cb7.Alias);
            Assert.Equal(CommitBlockStatus.Success, store.TryCommitBlock(cb7.Alias, _id_4_1, out _));

            // C(0) -> C(1) -> C(2)   -> C(3) -> C(4)
            //             |                 \-> C(4-1)
            //             \-> C(2-1) -> C(3-1) 

            var inbox = new BoundedInbox();
            var outbox = new BoundedInbox();

            var chainController = new ChainController(
                store,
                inbox,
                outbox,
                lineage => { });

            ClientId.Next();
            ClientId.Next();
            ClientId.Next();
            ClientId.Next();
            var c5 = ClientId.Next();

            return (store, inbox, outbox, chainController, c5);
        }

        [Fact]
        public (IChainStore, BoundedInbox, BoundedInbox, ChainController, ClientId, UncommittedBlockId) TestOpenBlock()
        {
            var (store, inbox, outbox, chainController, c5) = Setup();

            // C(0) -> C(1) -> C(2)   -> C(3) -> C(4)
            //             |                 \-> C(4-1)
            //             \-> C(2-1) -> C(3-1) 

            var request = OpenBlockRequest.From(_r1, c5, _id_3);
            inbox.TryWrite(request.Span);

            chainController.HandleRequest();

            var result = new OpenBlockResponse(outbox.Peek().Span);
            Assert.Equal(MessageKind.OpenBlockResponse, result.MessageHeader.MessageKind);
            Assert.Equal(_r1, result.MessageHeader.RequestId);
            Assert.Equal(c5, result.MessageHeader.ClientId);
            Assert.Equal(_4_2, result.Handle.ConvertToBlockAlias(c5.Mask));

            // C(0) -> C(1) -> C(2)   -> C(3) -> C(4)
            //             |                 \-> C(4-1)
            //             |                 \-> U(4-2)
            //             \-> C(2-1) -> C(3-1) 


            // TODO: [vermorel] Persistence unit tests should be isolated
            //var persister = new BlockchainPersister(TerabConfiguration.CommittedBlocksPersistencePath,
            //    TerabConfiguration.UncommittedBlocksPersistencePath);
            //var newChain = persister.ReadBlockchain();
            //Assert.Equal(chain.UncommittedBlockCount, newChain.UncommittedBlockCount);

            return (store, inbox, outbox, chainController, c5, result.UncommittedBlockId);
        }

        [Fact]
        public void TestOpenFirstBlock()
        {
            var (_, inbox, outbox, _, c5) = Setup();

            var store = new VolatileChainStore();

            var chainController = new ChainController(
                store,
                inbox,
                outbox,
                lineage => { });

            var request = OpenBlockRequest.From(
                _r1, c5, CommittedBlockId.GenesisParent);

            inbox.TryWrite(request.Span);

            chainController.HandleRequest();

            var result = new OpenBlockResponse(outbox.Peek().Span);

            Assert.Equal(MessageKind.OpenBlockResponse, result.MessageHeader.MessageKind);

            Assert.Equal(_r1, result.MessageHeader.RequestId);
            Assert.Equal(c5, result.MessageHeader.ClientId);
            Assert.Equal(new BlockAlias(0, 0), result.Handle.ConvertToBlockAlias(c5.Mask));

            // TODO: [vermorel] persistence unit tests should be isolated
            //var persister = new BlockchainPersister(TerabConfiguration.CommittedBlocksPersistencePath,
            //    TerabConfiguration.UncommittedBlocksPersistencePath);
            //var newChain = persister.ReadBlockchain();
            //Assert.Equal(chain.UncommittedBlockCount, newChain.UncommittedBlockCount);
            //Assert.Equal(chain.CommittedBlockCount, newChain.CommittedBlockCount);
        }

        [Fact]
        public void TestGetUncommittedBlockHandle()
        {
            // TODO: [vermorel] Remove all inter-test dependencies 

            var (_, inbox, outbox, chainController, c5, uncommittedBlockId) = TestOpenBlock();

            // C(0) -> C(1) -> C(2)   -> C(3) -> C(4)
            //             |                 \-> C(4-1)
            //             |                 \-> U(4-2)
            //             \-> C(2-1) -> C(3-1) 

            var request = GetBlockHandleRequest.From(_r1, c5, uncommittedBlockId);
            inbox.TryWrite(request.Span);

            chainController.HandleRequest();

            outbox.Next(); // because we did the TestOpenBlock in the beginning
            var result = new GetBlockHandleResponse(outbox.Peek().Span);

            Assert.Equal(MessageKind.BlockHandleResponse, result.MessageHeader.MessageKind);

            Assert.Equal(_r1, result.MessageHeader.RequestId);
            Assert.Equal(c5, result.MessageHeader.ClientId);
            Assert.Equal(_4_2, result.Handle.ConvertToBlockAlias(c5.Mask));
        }

        [Fact]
        public void TestGetCommittedBlockHandle()
        {
            var (_, inbox, outbox, chainController, c5) = Setup();

            var request = GetBlockHandleRequest.From(_r1, c5, _id_2);
            inbox.TryWrite(request.Span);

            chainController.HandleRequest();

            var result = new GetBlockHandleResponse(outbox.Peek().Span);

            Assert.Equal(MessageKind.BlockHandleResponse, result.MessageHeader.MessageKind);

            Assert.Equal(_r1, result.MessageHeader.RequestId);
            Assert.Equal(c5, result.MessageHeader.ClientId);

            Assert.Equal(_2, result.Handle.ConvertToBlockAlias(c5.Mask));
        }

        [Fact]
        public void TestCommitBlock()
        {
            var (_, inbox, outbox, chainController, c5, _) = TestOpenBlock();

            // C(0) -> C(1) -> C(2)   -> C(3) -> C(4)
            //             |                 \-> C(4-1)
            //             |                 \-> U(4-2)
            //             \-> C(2-1) -> C(3-1) 

            var request = CommitBlockRequest.From(
                _r1, c5, _4_2.ConvertToBlockHandle(c5.Mask), GetMockBlockId(42));

            inbox.TryWrite(request.Span);

            chainController.HandleRequest();

            outbox.Next();
            var result = new CommitBlockResponse(outbox.Peek().Span);

            Assert.Equal(MessageKind.CommitBlockResponse, result.MessageHeader.MessageKind);

            Assert.Equal(_r1, result.MessageHeader.RequestId);
            Assert.Equal(c5, result.MessageHeader.ClientId);

            // TODO: [vermorel] persistence unit tests should be isolated
            //var persister = new BlockchainPersister(TerabConfiguration.CommittedBlocksPersistencePath,
            //    TerabConfiguration.UncommittedBlocksPersistencePath);
            //var newChain = persister.ReadBlockchain();
            //Assert.True(chain.GetLastCommitted().BlockId.Equals(newChain.GetLastCommitted().BlockId));
        }

        [Fact]
        public void TestGetCommittedBlockInfo()
        {
            var (_, inbox, outbox, chainController, c5) = Setup();

            var request = GetBlockInfoRequest.From(_r1, c5, _1.ConvertToBlockHandle(c5.Mask));
            inbox.TryWrite(request.Span);

            chainController.HandleRequest();

            var response = new GetBlockInfoResponse(outbox.Peek().Span);

            Assert.Equal(MessageKind.BlockInfoResponse, response.MessageHeader.MessageKind);
            Assert.True(response.IsCommitted);

            Assert.Equal(_r1, response.MessageHeader.RequestId);
            Assert.Equal(c5, response.MessageHeader.ClientId);

            Assert.Equal(_id_1, response.CommittedBlockId);
            Assert.Equal(_1, response.Handle.ConvertToBlockAlias(c5.Mask));
            Assert.Equal(new BlockAlias(0, 0), response.Parent.ConvertToBlockAlias(c5.Mask));
            Assert.Equal(1, response.BlockHeight);
        }

        [Fact]
        public void TestGetUncommittedBlockInfo()
        {
            var (_, inbox, outbox, chainController, c5, uncommittedBlockId) = TestOpenBlock();

            // C(0) -> C(1) -> C(2)   -> C(3) -> C(4)
            //             |                 \-> C(4-1)
            //             |                 \-> U(4-2)
            //             \-> C(2-1) -> C(3-1) 

            var request = GetBlockInfoRequest.From(_r1, c5, _4_2.ConvertToBlockHandle(c5.Mask));
            inbox.TryWrite(request.Span);

            chainController.HandleRequest();

            outbox.Next();
            var response = new GetBlockInfoResponse(outbox.Peek().Span);

            Assert.Equal(MessageKind.BlockInfoResponse, response.MessageHeader.MessageKind);
            Assert.False(response.IsCommitted);

            Assert.Equal(_r1, response.MessageHeader.RequestId);
            Assert.Equal(c5, response.MessageHeader.ClientId);

            Assert.Equal(uncommittedBlockId, response.UncommittedBlockId);
            Assert.Equal(_4_2, response.Handle.ConvertToBlockAlias(c5.Mask));
            Assert.Equal(_3, response.Parent.ConvertToBlockAlias(c5.Mask));
            Assert.Equal(4, response.BlockHeight);
        }
    }
}