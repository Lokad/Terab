// Copyright Lokad 2018 under MIT BCH.
using System.Buffers.Binary;
using System.Runtime.InteropServices;
using Terab.Lib.Chains;
using Terab.Lib.Messaging;
using Terab.Lib.Messaging.Protocol;
using Terab.Lib.Tests.Mock;
using Xunit;

namespace Terab.Lib.Tests
{
    public class ApiIntegrationTests
    {
        private const int ShardCount = 4;

        private DispatchController _dispatcher;
        private ConnectionController _clientConn;
        private ChainController _chainController;
        private IChainStore _store;
        private readonly MockSocket _socket = new MockSocket();
        private readonly CommittedBlockId _zero = CommittedBlockId.GenesisParent;

        private readonly RequestId _r1 = new RequestId(1);

        private ClientId _c0; // if messages are sent with a clientId different from 0, the message is rejected
        private BlockHandleMask _handleMask;

        public void Setup()
        {
            var dispatchInbox = new BoundedInbox();
            var chainInbox = new BoundedInbox();
            var coinInboxes = new BoundedInbox[ShardCount];
            for (var i = 0; i < coinInboxes.Length; i++)
                coinInboxes[i] = new BoundedInbox();

            _dispatcher = new DispatchController(
                dispatchInbox,
                chainInbox,
                coinInboxes,
                new IdentityHash());

            // Configure the wake-up callbacks to do nothing.
            _dispatcher.OnBlockMessageDispatched = () => { };
            for (var i = 0; i < _dispatcher.OnCoinMessageDispatched.Length; i++)
                _dispatcher.OnCoinMessageDispatched[i] = () => { };

            _store = new VolatileChainStore();

            _chainController = new ChainController(
                _store,
                chainInbox,
                dispatchInbox,
                lineage => { });

            var c1 = ClientId.Next();
            _clientConn = new ConnectionController(dispatchInbox, _socket, c1);
            _handleMask = c1.Mask;

            _dispatcher.AddConnection(_clientConn);
            _dispatcher.OnConnectionAccepted = _ => { };
            _dispatcher.HandleNewConnection();

            _c0 = new ClientId(0);
        }

        [Fact]
        public void terab_utxo_get_committed_block()
        {
            terab_utxo_commit_block();

            _socket.ExpectReceive(data =>
            {
                data.Clear();
                Assert.True(data.Length == MessageHeader.SizeInBytes);

                var header = new MessageHeader(GetBlockHandleRequest.SizeInBytes, _r1, _c0,
                    MessageKind.GetBlockHandle);
                var headerStorage = new MessageHeader[1];
                headerStorage[0] = header;
                var headerBytes = MemoryMarshal.Cast<MessageHeader, byte>(headerStorage);
                headerBytes.CopyTo(data);

                return MessageHeader.SizeInBytes;
            });

            _socket.ExpectReceive(data =>
            {
                data.Clear();
                Assert.True(data.Length >= GetBlockHandleRequest.SizeInBytes - MessageHeader.SizeInBytes);

                var request = GetBlockHandleRequest.From(_r1, _c0, CommittedBlockId.Genesis);
                request.Span.Slice(16).CopyTo(data);

                return GetBlockHandleRequest.SizeInBytes - MessageHeader.SizeInBytes;
            });

            _socket.ExpectConnected(() => true);
            _socket.ExpectConnected(() => true);

            _socket.ExpectSend(data =>
            {
                Assert.Equal(GetBlockHandleResponse.SizeInBytes, data.Length);
                var response = new GetBlockHandleResponse(data);
                Assert.Equal(new BlockAlias(0, 0), response.Handle.ConvertToBlockAlias(_handleMask));
                return GetBlockHandleResponse.SizeInBytes;
            });

            Assert.True(_clientConn.HandleRequest());

            Assert.True(_dispatcher.HandleRequest());

            Assert.True(_chainController.HandleRequest());

            Assert.True(_dispatcher.HandleRequest());

            Assert.True(_clientConn.HandleResponse());

            _socket.ExpectAllDone();
        }

        [Fact]
        public void terab_uxto_get_blockinfo_uncommitted()
        {
            UncommittedBlockId blockUcid = terab_utxo_open_block();

            _socket.ExpectReceive(data =>
            {
                data.Clear();
                Assert.True(data.Length == MessageHeader.SizeInBytes);

                var header = new MessageHeader(GetBlockInfoRequest.SizeInBytes, _r1, _c0, MessageKind.GetBlockInfo);
                var headerStorage = new MessageHeader[1];
                headerStorage[0] = header;
                var headerBytes = MemoryMarshal.Cast<MessageHeader, byte>(headerStorage);
                headerBytes.CopyTo(data);

                return MessageHeader.SizeInBytes;
            });

            _socket.ExpectReceive(data =>
            {
                data.Clear();
                Assert.True(data.Length >= GetBlockInfoRequest.SizeInBytes - MessageHeader.SizeInBytes);

                var request = GetBlockInfoRequest.From(
                    _r1, _c0, new BlockAlias(0, 0).ConvertToBlockHandle(_handleMask));
                request.Span.Slice(16).CopyTo(data);

                return GetBlockInfoRequest.SizeInBytes - MessageHeader.SizeInBytes;
            });

            _socket.ExpectConnected(() => true);
            _socket.ExpectConnected(() => true);

            _socket.ExpectSend(data =>
            {
                Assert.Equal(GetBlockInfoResponse.SizeInBytes, data.Length);

                var response = new GetBlockInfoResponse(data);
                Assert.Equal(new BlockAlias(0, 0), response.Handle.ConvertToBlockAlias(_handleMask));
                Assert.Equal(BlockAlias.GenesisParent, response.Parent.ConvertToBlockAlias(_handleMask));
                Assert.Equal(0, response.BlockHeight);
                Assert.Equal(blockUcid, response.UncommittedBlockId);
                return GetBlockInfoResponse.SizeInBytes;
            });

            Assert.True(_clientConn.HandleRequest());

            Assert.True(_dispatcher.HandleRequest());

            Assert.True(_chainController.HandleRequest());

            Assert.True(_dispatcher.HandleRequest());

            Assert.True(_clientConn.HandleResponse());

            _socket.ExpectAllDone();
        }

        [Fact]
        public void terab_uxto_get_blockinfo_committed()
        {
            terab_utxo_commit_block();

            _socket.ExpectReceive(data =>
            {
                data.Clear();
                Assert.True(data.Length == MessageHeader.SizeInBytes);

                var header = new MessageHeader(GetBlockInfoRequest.SizeInBytes, _r1, _c0, MessageKind.GetBlockInfo);
                var headerStorage = new MessageHeader[1];
                headerStorage[0] = header;
                var headerBytes = MemoryMarshal.Cast<MessageHeader, byte>(headerStorage);
                headerBytes.CopyTo(data);

                return MessageHeader.SizeInBytes;
            });

            _socket.ExpectReceive(data =>
            {
                data.Clear();
                Assert.True(data.Length >= GetBlockInfoRequest.SizeInBytes - MessageHeader.SizeInBytes);

                var request = GetBlockInfoRequest.From(_r1, _c0,
                    new BlockAlias(0, 0).ConvertToBlockHandle(_handleMask));
                request.Span.Slice(16).CopyTo(data);

                return GetBlockInfoRequest.SizeInBytes - MessageHeader.SizeInBytes;
            });

            _socket.ExpectConnected(() => true);
            _socket.ExpectConnected(() => true);

            _socket.ExpectSend(data =>
            {
                Assert.Equal(GetBlockInfoResponse.SizeInBytes, data.Length);
                var response = new GetBlockInfoResponse(data);

                Assert.Equal(new BlockAlias(0, 0), response.Handle.ConvertToBlockAlias(_handleMask));
                Assert.Equal(BlockAlias.GenesisParent, response.Parent.ConvertToBlockAlias(_handleMask));
                Assert.Equal(0, response.BlockHeight);
                Assert.Equal(CommittedBlockId.Genesis, response.CommittedBlockId);

                return GetBlockInfoResponse.SizeInBytes;
            });

            Assert.True(_clientConn.HandleRequest());

            Assert.True(_dispatcher.HandleRequest());

            Assert.True(_chainController.HandleRequest());

            Assert.True(_dispatcher.HandleRequest());

            Assert.True(_clientConn.HandleResponse());

            _socket.ExpectAllDone();
        }

        [Fact]
        public void terab_utxo_get()
        {
            // TODO: functionality not implemented yet
        }

        [Fact]
        public UncommittedBlockId terab_utxo_open_block()
        {
            Setup();

            UncommittedBlockId? result = null;

            _socket.ExpectReceive(data =>
            {
                data.Clear();
                Assert.True(data.Length == MessageHeader.SizeInBytes);

                var header = new MessageHeader(OpenBlockRequest.SizeInBytes, _r1, _c0, MessageKind.OpenBlock);
                var headerStorage = new MessageHeader[1];
                headerStorage[0] = header;
                var headerBytes = MemoryMarshal.Cast<MessageHeader, byte>(headerStorage);
                headerBytes.CopyTo(data);

                return MessageHeader.SizeInBytes;
            });

            _socket.ExpectReceive(data =>
            {
                data.Clear();
                Assert.True(data.Length == OpenBlockRequest.SizeInBytes - MessageHeader.SizeInBytes);

                var request = OpenBlockRequest.From(_r1, _c0, _zero);
                request.Span.Slice(16).CopyTo(data);

                return OpenBlockRequest.SizeInBytes - MessageHeader.SizeInBytes;
            });

            _socket.ExpectConnected(() => true);
            _socket.ExpectConnected(() => true);

            _socket.ExpectSend(data =>
            {
                Assert.Equal(OpenBlockResponse.SizeInBytes, data.Length);
                var response = new OpenBlockResponse(data);
                Assert.Equal(OpenBlockResponse.SizeInBytes, BinaryPrimitives.ReadInt32LittleEndian(data));
                Assert.Equal(_r1, response.MessageHeader.RequestId); // request ID
                Assert.Equal(_c0, response.MessageHeader.ClientId); // client ID field empty
                result = response.UncommittedBlockId;
                return OpenBlockResponse.SizeInBytes;
            });

            Assert.True(_clientConn.HandleRequest());

            Assert.True(_dispatcher.HandleRequest());

            Assert.True(_chainController.HandleRequest());

            Assert.True(_dispatcher.HandleRequest());

            Assert.True(_clientConn.HandleResponse());

            _socket.ExpectAllDone();

            return result.Value;
        }

        [Fact]
        public void terab_utxo_write_txs()
        {
            // TODO: functionality not implemented yet
        }

        [Fact]
        public void terab_utxo_get_uncommitted_block()
        {
            var blockUcid = terab_utxo_open_block();

            _socket.ExpectReceive(data =>
            {
                data.Clear();
                Assert.True(data.Length == MessageHeader.SizeInBytes);

                var header = new MessageHeader(GetBlockHandleRequest.SizeInBytes,
                    _r1, _c0, MessageKind.GetBlockHandle);
                var headerStorage = new MessageHeader[1];
                headerStorage[0] = header;
                var headerBytes = MemoryMarshal.Cast<MessageHeader, byte>(headerStorage);
                headerBytes.CopyTo(data);

                return MessageHeader.SizeInBytes;
            });

            _socket.ExpectReceive(data =>
            {
                data.Clear();
                Assert.True(data.Length == GetBlockHandleRequest.SizeInBytes - MessageHeader.SizeInBytes);

                // TODO: [vermorel] bytes are off here
                var request = GetBlockHandleRequest.From(_r1, _c0, blockUcid);
                request.Span.Slice(16).CopyTo(data);

                return GetBlockHandleRequest.SizeInBytes - MessageHeader.SizeInBytes;
            });

            _socket.ExpectConnected(() => true);
            _socket.ExpectConnected(() => true);

            _socket.ExpectSend(data =>
            {
                Assert.Equal(GetBlockHandleResponse.SizeInBytes, data.Length);
                var response = new GetBlockHandleResponse(data);
                Assert.Equal(new BlockAlias(0, 0), response.Handle.ConvertToBlockAlias(_handleMask));
                return GetBlockHandleResponse.SizeInBytes;
            });

            Assert.True(_clientConn.HandleRequest());

            Assert.True(_dispatcher.HandleRequest());

            Assert.True(_chainController.HandleRequest());

            Assert.True(_dispatcher.HandleRequest());

            Assert.True(_clientConn.HandleResponse());

            _socket.ExpectAllDone();
        }

        [Fact]
        public void terab_utxo_commit_block()
        {
            terab_utxo_get_uncommitted_block();

            _socket.ExpectReceive(data =>
            {
                data.Clear();
                Assert.True(data.Length == MessageHeader.SizeInBytes);

                var header = new MessageHeader(CommitBlockRequest.SizeInBytes,
                    _r1, _c0, MessageKind.CommitBlock);
                var headerStorage = new MessageHeader[1];
                headerStorage[0] = header;
                var headerBytes = MemoryMarshal.Cast<MessageHeader, byte>(headerStorage);
                headerBytes.CopyTo(data);

                return MessageHeader.SizeInBytes;
            });

            _socket.ExpectReceive(data =>
            {
                data.Clear();
                Assert.True(data.Length >= CommitBlockRequest.SizeInBytes - MessageHeader.SizeInBytes);

                var request = CommitBlockRequest.From(
                    _r1, _c0, new BlockAlias(0, 0).ConvertToBlockHandle(_handleMask), CommittedBlockId.Genesis);

                request.Span.Slice(16).CopyTo(data);

                return CommitBlockRequest.SizeInBytes - MessageHeader.SizeInBytes;
            });

            _socket.ExpectConnected(() => true);
            _socket.ExpectConnected(() => true);

            _socket.ExpectSend(data =>
            {
                Assert.Equal(CommitBlockResponse.SizeInBytes, data.Length);
                return CommitBlockResponse.SizeInBytes;
            });

            Assert.True(_clientConn.HandleRequest());

            Assert.True(_dispatcher.HandleRequest());

            Assert.True(_chainController.HandleRequest());

            Assert.True(_dispatcher.HandleRequest());

            Assert.True(_clientConn.HandleResponse());

            _socket.ExpectAllDone();
        }
    }
}