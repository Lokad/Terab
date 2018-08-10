using System;
using System.Collections.Concurrent;
using Terab.Server;
using Terab.UTXO.Core.Blockchain;
using Terab.UTXO.Core.Hash;
using Terab.UTXO.Core.Messaging;
using Terab.UTXO.Core.Networking;
using Terab.UTXO.Core.Request;
using Terab.UTXO.Core.Response;
using Xunit;


namespace Terab.UTXO.Core.Tests
{
    public class ApiIntegrationTests
    {
        private Dispatcher _dispatcher;
        private ClientConnection _clientConn;
        private ControllerThread _controllerThread;
        private SimpleBlockchain _chain;
        private readonly MockSocket _socket = new MockSocket();
        private readonly BlockAlias _zero = new BlockAlias(0);

        public void Setup()
        {
            var sharedInboxDispatcherController = BoundedInbox.Create();
            var sharedOutboxDispatcherController = BoundedInbox.Create();
            var clientQueue = new ConcurrentQueue<ClientConnection>();
            _dispatcher = new Dispatcher(clientQueue, 3, sharedOutboxDispatcherController, new BoundedInbox[4],
                sharedInboxDispatcherController);
            _chain = new SimpleBlockchain();
            _controllerThread = new ControllerThread(_chain, default(OptimizedLineage), sharedInboxDispatcherController,
                sharedOutboxDispatcherController);
            _clientConn = new ClientConnection(_socket, ClientId.Next(), ClientServerMessage.MaxSizeInBytes);

            clientQueue.Enqueue(_clientConn);
            _dispatcher.DequeueNewClients();

            // TODO there should really be three threads, one for each component. However, I do not know how to test this.
        }

        [Fact]
        public void terab_connect()
        {
            // TODO functionality not implemented yet
        }

        [Fact]
        public void terab_utxo_get_block()
        {
            terab_utxo_commit_block();

            _socket.ExpectConnected(() => true);
            _socket.ExpectConnected(() => true);
            _socket.ExpectConnected(() => true);
            _socket.ExpectAvailable(() => GetBlockHandle.SizeInBytes);

            _socket.ExpectReceive(data =>
            {
                data.Clear();
                Assert.True(data.Length >= GetBlockHandle.SizeInBytes);

                var getHandle = new GetBlockHandle(1, 0, BlockId.Genesis);
                MessageSerializers.ClientSerializeGetBlockHandle(getHandle, data);

                return GetBlockHandle.SizeInBytes;
            });

            _socket.ExpectConnected(() => true);
            _socket.ExpectAvailable(() => 0);

            _socket.ExpectConnected(() => true);
            _socket.ExpectSend(data =>
            {
                Assert.Equal(BlockHandleResponse.SizeInBytes, data.Length);
                BlockHandleResponse response = MessageSerializers.ClientDeserializeBlockHandleResponse(data);
                Assert.Equal(BlockAlias.Genesis, response.BlockHandle);
                return BlockHandleResponse.SizeInBytes;
            });

            _dispatcher.ListenToConnections();

            _controllerThread.DoWork();

            _dispatcher.SendResponses();

            _socket.ExpectAllDone();
        }

        [Fact]
        public void terab_uxto_get_blockinfo_uncommitted()
        {
            TempBlockId blockUcid = terab_utxo_open_block();

            _socket.ExpectConnected(() => true);
            _socket.ExpectConnected(() => true);
            _socket.ExpectConnected(() => true);
            _socket.ExpectAvailable(() => GetBlockInformation.SizeInBytes);

            _socket.ExpectReceive(data =>
            {
                data.Clear();
                Assert.True(data.Length >= GetBlockInformation.SizeInBytes);

                var getInfo = new GetBlockInformation(1, 0, BlockAlias.Genesis);
                MessageSerializers.ClientSerializeGetBlockInformation(getInfo, data);

                return GetBlockInformation.SizeInBytes;
            });

            _socket.ExpectConnected(() => true);
            _socket.ExpectAvailable(() => 0);

            _socket.ExpectConnected(() => true);
            _socket.ExpectSend(data =>
            {
                Assert.Equal(UncommittedBlockInformation.SizeInBytes, data.Length);
                var actualBlockInfo = MessageSerializers.ClientDeserializeUncommittedBlockInfo(data);
                Assert.Equal(BlockAlias.Genesis, actualBlockInfo.Alias);
                Assert.Equal(BlockAlias.GenesisParent, actualBlockInfo.Parent);
                Assert.Equal(0, actualBlockInfo.BlockHeight);
                Assert.Equal(blockUcid, actualBlockInfo.UncommittedBlockId);
                return UncommittedBlockInformation.SizeInBytes;
            });

            _dispatcher.ListenToConnections();

            _controllerThread.DoWork();

            _dispatcher.SendResponses();

            _socket.ExpectAllDone();
        }

        [Fact]
        public void terab_uxto_get_blockinfo_committed()
        {
            terab_utxo_commit_block();

            _socket.ExpectConnected(() => true);
            _socket.ExpectConnected(() => true);
            _socket.ExpectConnected(() => true);
            _socket.ExpectAvailable(() => GetBlockInformation.SizeInBytes);

            _socket.ExpectReceive(data =>
            {
                data.Clear();
                Assert.True(data.Length >= GetBlockInformation.SizeInBytes);

                var getInfo = new GetBlockInformation(1, 0, BlockAlias.Genesis);
                // Serialize message to put it into the inbox
                MessageSerializers.ClientSerializeGetBlockInformation(getInfo, data);
                return GetBlockInformation.SizeInBytes;
            });

            _socket.ExpectConnected(() => true);
            _socket.ExpectAvailable(() => 0);

            _socket.ExpectConnected(() => true);
            _socket.ExpectSend(data =>
            {
                Assert.Equal(CommittedBlockInformation.SizeInBytes, data.Length);
                var commitedInfo = MessageSerializers.ClientDeserializeCommittedBlockInfo(data);

                Assert.Equal(BlockAlias.Genesis, commitedInfo.Alias);
                Assert.Equal(BlockAlias.GenesisParent, commitedInfo.Parent);
                Assert.Equal(0, commitedInfo.BlockHeight);
                Assert.Equal(BlockId.Genesis, commitedInfo.BlockId);

                return CommittedBlockInformation.SizeInBytes;
            });

            _dispatcher.ListenToConnections();

            _controllerThread.DoWork();

            _dispatcher.SendResponses();

            _socket.ExpectAllDone();
        }

        [Fact]
        public void terab_utxo_get()
        {
            // TODO functionality not implemented yet
        }

        [Fact]
        public TempBlockId terab_utxo_open_block()
        {
            Setup();

            TempBlockId? result = null;

            _socket.ExpectConnected(() => true);
            _socket.ExpectConnected(() => true);
            _socket.ExpectConnected(() => true);
            _socket.ExpectAvailable(() => OpenBlock.SizeInBytes);

            _socket.ExpectReceive(data =>
            {
                data.Clear();
                Assert.True(data.Length >= OpenBlock.SizeInBytes);

                var openBlock = new OpenBlock(1, 0, _zero);
                // Serialize message to put it into the inbox
                MessageSerializers.ClientSerializeOpenBlock(openBlock, data);
                return OpenBlock.SizeInBytes;
            });

            _socket.ExpectConnected(() => true);
            _socket.ExpectAvailable(() => 0);

            _socket.ExpectConnected(() => true);
            _socket.ExpectSend(data =>
            {
                Assert.Equal(OpenedBlock.SizeInBytes, data.Length);
                OpenedBlock openedBlock = MessageSerializers.ClientDeserializeOpenedBlock(data);
                Assert.Equal(OpenedBlock.SizeInBytes, BitConverter.ToInt32(data));
                Assert.Equal((uint) 1, openedBlock.RequestId); // request ID
                Assert.Equal((uint) 0, openedBlock.ClientId); // client ID field empty
                result = openedBlock.UncommittedBlockId;
                return OpenedBlock.SizeInBytes;
            });

            _dispatcher.ListenToConnections();

            _controllerThread.DoWork();

            _dispatcher.SendResponses();

            _socket.ExpectAllDone();

            return result.Value;
        }

        [Fact]
        public void terab_utxo_write_txs()
        {
            // TODO functionality not implemented yet
        }

        [Fact]
        public void terab_utxo_get_uncommitted_block()
        {
            var blockUcid = terab_utxo_open_block();

            _socket.ExpectConnected(() => true);
            _socket.ExpectConnected(() => true);
            _socket.ExpectConnected(() => true);
            _socket.ExpectAvailable(() => GetUncommittedBlockHandle.SizeInBytes);

            _socket.ExpectReceive(data =>
            {
                data.Clear();
                Assert.True(data.Length >= GetUncommittedBlockHandle.SizeInBytes);

                var getUHandle = new GetUncommittedBlockHandle(1, 0, blockUcid);
                // Serialize message to put it into the inbox
                MessageSerializers.ClientSerializeGetUncommittedBlockHandle(getUHandle, data);

                return GetUncommittedBlockHandle.SizeInBytes;
            });

            _socket.ExpectConnected(() => true);
            _socket.ExpectAvailable(() => 0);

            _socket.ExpectConnected(() => true);
            _socket.ExpectSend(data =>
            {
                Assert.Equal(BlockHandleResponse.SizeInBytes, data.Length);
                BlockHandleResponse response = MessageSerializers.ClientDeserializeBlockHandleResponse(data);
                Assert.Equal(BlockAlias.Genesis, response.BlockHandle);
                return BlockHandleResponse.SizeInBytes;
            });

            _dispatcher.ListenToConnections();

            _controllerThread.DoWork();

            _dispatcher.SendResponses();

            _socket.ExpectAllDone();
        }

        [Fact]
        public void terab_utxo_commit_block()
        {
            terab_utxo_get_uncommitted_block();

            _socket.ExpectConnected(() => true);
            _socket.ExpectConnected(() => true);
            _socket.ExpectConnected(() => true);
            _socket.ExpectAvailable(() => CommitBlock.SizeInBytes);

            _socket.ExpectReceive(data =>
            {
                data.Clear();
                Assert.True(data.Length >= CommitBlock.SizeInBytes);

                var commit = new CommitBlock(1, 0, BlockAlias.Genesis, BlockId.Genesis);
                // Serialize message to put it into the inbox
                MessageSerializers.ClientSerializeCommitBlock(commit, data);

                return CommitBlock.SizeInBytes;
            });

            _socket.ExpectConnected(() => true);
            _socket.ExpectAvailable(() => 0);

            _socket.ExpectConnected(() => true);
            _socket.ExpectSend(data =>
            {
                Assert.Equal(EverythingOkResponse.SizeInBytes, data.Length);
                return EverythingOkResponse.SizeInBytes;
            });

            _dispatcher.ListenToConnections();

            _controllerThread.DoWork();

            _dispatcher.SendResponses();

            _socket.ExpectAllDone();
        }
    }
}