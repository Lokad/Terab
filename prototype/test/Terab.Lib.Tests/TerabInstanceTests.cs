// Copyright Lokad 2018 under MIT BCH.
using System;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using Terab.Lib.Chains;
using Terab.Lib.Coins;
using Terab.Lib.Messaging;
using Terab.Lib.Messaging.Protocol;
using Terab.Lib.Networking;
using Terab.Lib.Tests.Mock;
using Xunit;
using Xunit.Abstractions;
using CommitBlockStatus = Terab.Lib.Messaging.CommitBlockStatus;
using OpenBlockStatus = Terab.Lib.Messaging.OpenBlockStatus;

namespace Terab.Lib.Tests
{
    public class TerabInstanceTests
    {
        private readonly ILog _log;

        public TerabInstanceTests(ITestOutputHelper output)
        {
            _log = new XLog(output);
        }

        private TerabInstance GetInstance()
        {
            var instance = new TerabInstance(_log);

            instance.OutpointHash = new IdentityHash();

            instance.ChainStore = new VolatileChainStore();

            instance.CoinStores = new ICoinStore[4];
            for (var i = 0; i < instance.CoinStores.Length; i++)
                instance.CoinStores[i] = new VolatileCoinStore();

            instance.SetupControllers();

            return instance;
        }

        [Fact]
        public void StartStop()
        {
            var instance = GetInstance();
            instance.Start();
            Thread.Sleep(100);
            instance.Stop();
        }

        [Fact]
        public unsafe void OpenCommit()
        {
            var instance = GetInstance();
            instance.Start();

            var localEndpoint = new IPEndPoint(IPAddress.Loopback, instance.Port);
            var rawSocket = new Socket(localEndpoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            rawSocket.Connect(localEndpoint);

            var socket = new SocketLikeAdapter(rawSocket);

            try
            {
                // Open block
                var openBlockRequest = OpenBlockRequest.ForGenesis(RequestId.MinRequestId);
                socket.Send(openBlockRequest.Span);

                var openBlockResponse = new OpenBlockResponse(new byte[OpenBlockResponse.SizeInBytes]);
                socket.Receive(openBlockResponse.Span);

                Assert.Equal(OpenBlockStatus.Success, openBlockResponse.Status);

                // Commit block
                var blockId = new CommittedBlockId();
                for (var i = 0; i < CommittedBlockId.SizeInBytes; i++)
                    blockId.Data[i] = (byte) i;

                var commitBlockRequest = CommitBlockRequest.From(
                    RequestId.MinRequestId,
                    ClientId.MinClientId,
                    openBlockResponse.Handle,
                    blockId);
                socket.Send(commitBlockRequest.Span);

                var commitBlockResponse = new CommitBlockResponse(new byte[CommitBlockResponse.SizeInBytes]);
                socket.Receive(commitBlockResponse.Span);

                Assert.Equal(CommitBlockStatus.Success, commitBlockResponse.Status);

                // Get block info
                var getBlockInfoRequest = GetBlockInfoRequest.From(
                    RequestId.MinRequestId,
                    ClientId.MinClientId,
                    openBlockResponse.Handle);
                socket.Send(getBlockInfoRequest.Span);

                var getBlockInfoResponse =
                    new GetBlockInfoResponse(new byte[GetBlockInfoResponse.SizeInBytes]);
                socket.Receive(getBlockInfoResponse.Span);

                Assert.True(getBlockInfoResponse.CommittedBlockId.Equals(blockId));
            }
            finally
            {
                socket.Close();
                instance.Stop();
            }
        }

        private static unsafe Coin GetCoin(Random rand, BlockAlias production)
        {
            var coin = new Coin(new byte[4096]);

            var outpoint = new Outpoint();
            for (var i = 0; i < 32; i++)
                outpoint.TxId[i] = (byte) rand.Next();
            outpoint.TxIndex = rand.Next();

            coin.Outpoint = outpoint;

            var events = new[] {new CoinEvent(production, CoinEventKind.Production)};
            coin.SetEvents(events);

            var script = new byte[100];
            rand.NextBytes(script);

            var payload = new Payload(new byte[100 + sizeof(uint) + sizeof(ulong) + sizeof(int)]);
            payload.NLockTime = (uint) rand.Next();
            payload.Satoshis = (ulong) rand.Next();
            payload.Append(script);

            coin.SetPayload(payload);

            return coin;
        }

        [Fact]
        public void OpenGetBlockHandle()
        {
            var instance = GetInstance();
            instance.Start();

            var localEndpoint = new IPEndPoint(IPAddress.Loopback, instance.Port);
            var rawSocket = new Socket(localEndpoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            rawSocket.Connect(localEndpoint);

            var socket = new SocketLikeAdapter(rawSocket);

            try
            {
                // Open block
                var openBlockRequest = OpenBlockRequest.ForGenesis(RequestId.MinRequestId);
                socket.Send(openBlockRequest.Span);

                var openBlockResponse = new OpenBlockResponse(new byte[OpenBlockResponse.SizeInBytes]);
                socket.Receive(openBlockResponse.Span);

                Assert.Equal(OpenBlockStatus.Success, openBlockResponse.Status);

                // Get block handle
                var clientId = new ClientId();
                var blockHandleRequest = GetBlockHandleRequest.From(
                    new RequestId(1), clientId, openBlockResponse.UncommittedBlockId);
                socket.Send(blockHandleRequest.Span);

                var blockHandleResponse = new GetBlockHandleResponse(new byte[GetBlockHandleResponse.SizeInBytes]);
                socket.Receive(blockHandleResponse.Span);

                Assert.Equal(GetBlockHandleStatus.Success, blockHandleResponse.Status);
                Assert.Equal(openBlockResponse.Handle, blockHandleResponse.Handle);
                Assert.Equal(default(ClientId), blockHandleResponse.MessageHeader.ClientId);
                Assert.Equal(new RequestId(1), blockHandleResponse.MessageHeader.RequestId);
            }
            finally
            {
                socket.Close();
                instance.Stop();
            }
        }

        [Fact]
        public void WriteReadCoins()
        {
            var instance = GetInstance();

            var clientId = new ClientId(123);
            instance.Listener.GetNextClientId = () => clientId;

            instance.Start();

            var localEndpoint = new IPEndPoint(IPAddress.Loopback, instance.Port);
            var rawSocket = new Socket(localEndpoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            rawSocket.Connect(localEndpoint);

            var socket = new SocketLikeAdapter(rawSocket);

            try
            {
                // Open block
                var openBlockRequest = OpenBlockRequest.ForGenesis(RequestId.MinRequestId);
                socket.Send(openBlockRequest.Span);

                var openBlockResponse = new OpenBlockResponse(new byte[OpenBlockResponse.SizeInBytes]);
                socket.Receive(openBlockResponse.Span);

                Assert.Equal(OpenBlockStatus.Success, openBlockResponse.Status);

                var reqNum = 0U;

                // Write 100 coins into this open block and save the outpoints to read them later
                var outpointSaverComm = new Outpoint[100];
                var randyRandom = new Random(42);
                var produceCoinResponseBuffer = new byte[ProduceCoinResponse.SizeInBytes];
                var consumeCoinResponseBuffer = new byte[ConsumeCoinResponse.SizeInBytes];
                for (var i = 0; i < 100; i++)
                {
                    var coinToWrite = GetCoin(randyRandom, openBlockResponse.Handle.ConvertToBlockAlias(clientId.Mask));

                    outpointSaverComm[i] = coinToWrite.Outpoint;

                    var writeCoinRequest = ProduceCoinRequest.From(
                        new RequestId(++reqNum), clientId, coinToWrite.Outpoint,
                        (OutpointFlags) coinToWrite.OutpointFlags,
                        openBlockResponse.Handle
                            .ConvertToBlockAlias(clientId.Mask), // reconverted to BlockHandle after object construction
                        coinToWrite.Payload.Satoshis, coinToWrite.Payload.NLockTime,
                        coinToWrite.Payload.Script, clientId.Mask);
                    socket.Send(writeCoinRequest.Span);

                    var writeCoinResponse = new ProduceCoinResponse(produceCoinResponseBuffer);
                    socket.Receive(writeCoinResponse.Span);

                    Assert.Equal(ChangeCoinStatus.Success, writeCoinResponse.Status);

                    // every second coin, write a consumption
                    if (i % 2 == 0)
                    {
                        var writeCoinConsRequest = ConsumeCoinRequest.From(
                            new RequestId(++reqNum), ref coinToWrite.Outpoint,
                            // reconverted to BlockHandle after object construction
                            openBlockResponse.Handle.ConvertToBlockAlias(clientId.Mask),
                            clientId.Mask);
                        socket.Send(writeCoinConsRequest.Span);

                        var consumeCoinResponse = new ConsumeCoinResponse(consumeCoinResponseBuffer);
                        socket.Receive(consumeCoinResponse.Span);

                        Assert.Equal(ChangeCoinStatus.Success, consumeCoinResponse.Status);
                    }
                } // end of write coins

                // Commit the block
                var commitBlockRequest = CommitBlockRequest.From(new RequestId(++reqNum), clientId,
                    openBlockResponse.Handle, CommittedBlockId.Genesis);
                socket.Send(commitBlockRequest.Span);

                var commitBlockResponse = new CommitBlockResponse(new byte[CommitBlockResponse.SizeInBytes]);
                socket.Receive(commitBlockResponse.Span);

                Assert.Equal(CommitBlockStatus.Success, commitBlockResponse.Status);

                // Open a new block
                var openBlockRequest2 =
                    OpenBlockRequest.From(new RequestId(++reqNum), clientId, CommittedBlockId.Genesis);
                socket.Send(openBlockRequest2.Span);

                var openBlockResponse2 = new OpenBlockResponse(new byte[OpenBlockResponse.SizeInBytes]);
                socket.Receive(openBlockResponse2.Span);

                Assert.Equal(OpenBlockStatus.Success, openBlockResponse2.Status);

                // Write 100 coins into the new block
                var outpointSaverUncomm = new Outpoint[100];

                for (var i = 0; i < 100; i++)
                {
                    var coinToWrite = GetCoin(randyRandom,
                        openBlockResponse2.Handle.ConvertToBlockAlias(clientId.Mask));
                    outpointSaverUncomm[i] = coinToWrite.Outpoint;
                    var writeCoinRequest = ProduceCoinRequest.From(
                        new RequestId(++reqNum), clientId, coinToWrite.Outpoint,
                        (OutpointFlags) coinToWrite.OutpointFlags,
                        openBlockResponse2.Handle
                            .ConvertToBlockAlias(clientId.Mask), // reconverted to BlockHandle after object construction
                        coinToWrite.Payload.Satoshis, coinToWrite.Payload.NLockTime,
                        coinToWrite.Payload.Script, clientId.Mask);
                    socket.Send(writeCoinRequest.Span);

                    var writeCoinResponse = new ProduceCoinResponse(produceCoinResponseBuffer);
                    socket.Receive(writeCoinResponse.Span);
                    Assert.Equal(ChangeCoinStatus.Success, writeCoinResponse.Status);

                    // every second coin, write a consumption
                    if (i % 2 == 0)
                    {
                        var writeCoinConsRequest = ConsumeCoinRequest.From(
                            new RequestId(++reqNum), ref coinToWrite.Outpoint,
                            openBlockResponse2.Handle
                                .ConvertToBlockAlias(clientId
                                    .Mask), // reconverted to BlockHandle after object construction
                            clientId.Mask);
                        socket.Send(writeCoinConsRequest.Span);

                        var consumeCoinResponse = new ConsumeCoinResponse(consumeCoinResponseBuffer);
                        socket.Receive(consumeCoinResponse.Span);

                        Assert.Equal(ChangeCoinStatus.Success, consumeCoinResponse.Status);
                    }
                } // end of second write coins

                // Read coins from the committed block
                var headerBuffer = new byte[MessageHeader.SizeInBytes];

                for (var i = 0; i < 100; i++)
                {
                    var readCoinRequest = GetCoinRequest.From(
                        new RequestId(++reqNum), clientId, outpointSaverComm[i],
                        openBlockResponse.Handle.ConvertToBlockAlias(clientId.Mask),
                        clientId.Mask); // reconverted to BlockHandle after object construction
                    socket.Send(readCoinRequest.Span);

                    socket.Receive(headerBuffer);
                    var header = MemoryMarshal.Cast<byte, MessageHeader>(headerBuffer)[0];
                    Assert.Equal(MessageKind.GetCoinResponse, header.MessageKind);
                    var readCoinResponseBuffer = new byte[header.MessageSizeInBytes];
                    headerBuffer.CopyTo(readCoinResponseBuffer, 0);
                    socket.Receive(readCoinResponseBuffer.AsSpan(16));

                    var readCoinResponse = new GetCoinResponse(readCoinResponseBuffer);

                    Assert.Equal(header.MessageSizeInBytes, readCoinResponse.Span.Length);
                    Assert.Equal(openBlockResponse.Handle, readCoinResponse.Production);
                    if (i % 2 == 0)
                    {
                        Assert.Equal(openBlockResponse.Handle.Value, readCoinResponse.Consumption.Value);
                    }
                    else
                    {
                        Assert.Equal(readCoinResponse.Consumption.Value,
                            BlockAlias.Undefined.ConvertToBlockHandle(clientId.Mask).Value);
                    }

                    Assert.Equal(outpointSaverComm[i], readCoinResponse.Outpoint);
                }

                // Read coins from the uncommitted block
                for (var i = 0; i < 100; i++)
                {
                    var readCoinRequest = GetCoinRequest.From(
                        new RequestId(++reqNum), clientId, outpointSaverUncomm[i],
                        openBlockResponse2.Handle.ConvertToBlockAlias(clientId.Mask),
                        clientId.Mask); // reconverted to BlockHandle after object construction
                    socket.Send(readCoinRequest.Span);

                    socket.Receive(headerBuffer);
                    var header = MemoryMarshal.Cast<byte, MessageHeader>(headerBuffer)[0];
                    Assert.Equal(MessageKind.GetCoinResponse, header.MessageKind);
                    var readCoinResponseBuffer = new byte[header.MessageSizeInBytes];
                    headerBuffer.CopyTo(readCoinResponseBuffer, 0);
                    socket.Receive(readCoinResponseBuffer.AsSpan(16));

                    var readCoinResponse = new GetCoinResponse(readCoinResponseBuffer);

                    Assert.Equal(header.MessageSizeInBytes, readCoinResponse.Span.Length);
                    Assert.Equal(openBlockResponse2.Handle.Value, readCoinResponse.Production.Value);
                    if (i % 2 == 0)
                    {
                        Assert.Equal(openBlockResponse2.Handle.Value, readCoinResponse.Consumption.Value);
                    }
                    else
                    {
                        Assert.Equal(readCoinResponse.Consumption.Value,
                            BlockAlias.Undefined.ConvertToBlockHandle(clientId.Mask).Value);
                    }

                    Assert.Equal(outpointSaverUncomm[i], readCoinResponse.Outpoint);
                }

                // Read a coin from the future
                var readFutureCoinRequest = GetCoinRequest.From(
                    new RequestId(++reqNum), clientId, outpointSaverUncomm[0],
                    openBlockResponse.Handle.ConvertToBlockAlias(clientId.Mask),
                    clientId.Mask); // reconverted to BlockHandle after object construction
                socket.Send(readFutureCoinRequest.Span);

                socket.Receive(headerBuffer);
                {
                    var header = MemoryMarshal.Cast<byte, MessageHeader>(headerBuffer)[0];
                    var readFutureCoinResponseBuffer = new byte[header.MessageSizeInBytes];
                    var readFutureCoinResponse = new GetCoinResponse(readFutureCoinResponseBuffer);
                    headerBuffer.CopyTo(readFutureCoinResponseBuffer, 0);
                    socket.Receive(readFutureCoinResponseBuffer.AsSpan(16));

                    Assert.Equal(GetCoinStatus.OutpointNotFound, readFutureCoinResponse.Status);
                }
            }
            finally
            {
                socket.Close();
                instance.Stop();
            }
        }
    }
}