// Copyright Lokad 2018 under MIT BCH.
using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Threading;
using CommandLine;
using Terab.Lib;
using Terab.Lib.Messaging;
using Terab.Lib.Messaging.Protocol;
using Terab.Lib.Networking;

namespace Terab.Benchmark
{
    public class Program
    {
        private const int CoinIoCount = 10_000_000;
        private const int BatchSize = 512;
        private const int EpochSize = 512 * BatchSize; // keep 'EpochSize' as a multiple of 'BatchSize'

        public static void Main(string[] args)
        {
            Parser.Default.ParseArguments<InitOptions, RunOptions, RRunOptions>(args)
                .WithParsed<InitOptions>(Init)
                .WithParsed<RunOptions>(Run)
                .WithParsed<RRunOptions>(RemoteRun);
        }

        private static void Init(InitOptions options)
        {
            var log = new ConsoleLog();

            var config = new TerabConfig
            {
                Layer1Path = options.Layer1Path,
                Layer3Path = string.Empty,
                Port = 0, // auto-selecting port
            };

            TerabInstance.InitializeFiles(config, (long) (options.Layer1SizeInGB * 1e9), log: log);

            var instance = new TerabInstance(log);

            // Inner initialization triggered by 'SetupStores'.
            instance.SetupStores(config);
        }

        private static void Run(RunOptions options)
        {
            var log = new ConsoleLog();
            var instance = new TerabInstance(log);

            var config = new TerabConfig
            {
                Layer1Path = options.Layer1Path,
                Layer3Path = string.Empty,
                Port = 0, // auto-selecting port
            };

            instance.SetupStores(config);
            instance.SetupNetwork(config);
            instance.SetupControllers();

            try
            {
                instance.Start();
                Thread.Sleep(500);

                var localEndpoint = new IPEndPoint(IPAddress.Loopback, instance.Port);
                DoBenchmark(localEndpoint, log);
            }
            finally
            {
                instance.Stop();
            }
        }

        private static void RemoteRun(RRunOptions options)
        {
            var log = new ConsoleLog();

            var ipAddress = IPAddress.Parse(options.IpAddress);
            var endpoint = new IPEndPoint(ipAddress, options.Port);

            DoBenchmark(endpoint, log);
        }

        private static void DoBenchmark(IPEndPoint endpoint, ILog log)
        {
            var rand = new Random(); // non-deterministic on purpose

            var genWatch = new Stopwatch();
            genWatch.Start();

            var generator = new CoinGenerator(rand);
            generator.GenerateSketches(CoinIoCount);

            log.Log(LogSeverity.Info,
                $"{CoinIoCount} coin I/Os generated in " + (genWatch.ElapsedMilliseconds / 1000f) + " seconds");

            var rawSocket = new Socket(endpoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            rawSocket.Connect(endpoint);
            var socket = new SocketLikeAdapter(rawSocket);

            EnsureGenesisExists(socket);

            var handle = OpenBlock(CommittedBlockId.Genesis, socket, log);

            // Process coins
            var requestPool = new SpanPool<byte>(1024 * 1024);
            var responseBuffer = new Span<byte>(new byte[4096]);

            var watch = new Stopwatch();
            var commitWatch = new Stopwatch();
            var outpointSeed = generator.Random.Next();
            for (var i = 0; i < generator.CoinSketches.Length; i++)
            {
                if (i % EpochSize == 0)
                {
                    watch.Reset();
                    watch.Start();
                }

                var sketch = generator.CoinSketches[i];
                var outpoint = GetOutpoint(sketch, outpointSeed);

                if (sketch.IsProduction)
                {
                    // intended side effect on 'requestPool'
                    var writeCoinRequest = new ProduceCoinRequest(
                        new RequestId((uint) i),
                        ref outpoint,
                        OutpointFlags.None,
                        handle,
                        satoshis: 123,
                        nLockTime: 42,
                        scriptSizeInBytes: 100,
                        requestPool);

                    var script = writeCoinRequest.Script;
                    script[0] = (byte) i;
                }

                if (sketch.IsRead)
                {
                    // intended side effect on 'requestPool'
                    var readCoinRequest =
                        new GetCoinRequest(new RequestId((uint) i), ref outpoint, handle, requestPool);
                }

                if (sketch.IsConsumption)
                {
                    // intended side effect on 'requestPool'
                    var writeCoinRequest = new ConsumeCoinRequest(
                        new RequestId((uint) i), ref outpoint, handle, requestPool);
                }

                if (i % BatchSize == BatchSize - 1)
                {
                    // send all the requests as a batch
                    socket.Send(requestPool.Allocated());
                    requestPool.Reset();

                    for (var j = 0; j < BatchSize; j++)
                    {
                        socket.Receive(responseBuffer.Slice(0, 4));
                        var responseSize = responseBuffer[0];
                        socket.Receive(responseBuffer.Slice(4, responseSize - 4));
                    }
                }

                if (i % EpochSize == EpochSize - 1)
                {
                    watch.Stop();

                    var nextBlockId = GetBlockId();

                    commitWatch.Reset();
                    commitWatch.Start();
                    CommitBlock(handle, nextBlockId, socket);
                    commitWatch.Stop();

                    handle = OpenBlock(nextBlockId, socket, log);

                    var elapsed = watch.ElapsedMilliseconds / 1000.0; // seconds

                    var iops = (int) (EpochSize / elapsed);

                    log.Log(LogSeverity.Info,
                        $"Epoch {i / EpochSize} at {iops} IOps. Commit in {commitWatch.Elapsed.TotalMilliseconds} ms.");
                }
            }
        }

        private static unsafe Outpoint GetOutpoint(CoinSketch sketch, int seed)
        {
            var outpoint = new Outpoint();
            for (var i = 0; i < 4; i++)
                outpoint.TxId[i] = (byte) (sketch.CoinId >> (i * 8 + 2));
            for (var i = 0; i < 4; i++)
                outpoint.TxId[i + 4] = (byte) (seed >> (i * 8));

            return outpoint;
        }

        private static unsafe CommittedBlockId GetBlockId()
        {
            var buffer = new byte[32];
            RandomNumberGenerator.Fill(buffer);

            var blockId = new CommittedBlockId();
            for (var i = 0; i < 32; i++)
                blockId.Data[i] = buffer[i];

            return blockId;
        }

        private static void EnsureGenesisExists(ISocketLike socket)
        {
            var openGenesisRequest = OpenBlockRequest.ForGenesis(RequestId.MinRequestId);
            socket.Send(openGenesisRequest.Span);

            var openGenesisResponse = new OpenBlockResponse(new byte[OpenBlockResponse.SizeInBytes]);
            socket.Receive(openGenesisResponse.Span);

            if (openGenesisResponse.Status == OpenBlockStatus.Success)
            {
                var commitGenesisRequest = CommitBlockRequest.From(
                    RequestId.MinRequestId, ClientId.MinClientId, openGenesisResponse.Handle, CommittedBlockId.Genesis);
                socket.Send(commitGenesisRequest.Span);

                var commitGenesisResponse = new CommitBlockResponse(new byte[CommitBlockResponse.SizeInBytes]);
                socket.Receive(commitGenesisResponse.Span);
            }
        }

        private static BlockHandle OpenBlock(CommittedBlockId parentId, ISocketLike socket, ILog log)
        {
            var openBlockRequest = OpenBlockRequest.From(
                RequestId.MinRequestId, ClientId.MinClientId, parentId);
            socket.Send(openBlockRequest.Span);

            var openBlockResponse = new OpenBlockResponse(new byte[OpenBlockResponse.SizeInBytes]);
            socket.Receive(openBlockResponse.Span);

            if (openBlockResponse.Status != OpenBlockStatus.Success)
                throw new InvalidOperationException("Failed to open block.");

            log.Log(LogSeverity.Info, "Block opened, writing coins...");

            return openBlockResponse.Handle;
        }

        private static void CommitBlock(BlockHandle handle, CommittedBlockId blockId, ISocketLike socket)
        {
            var request = CommitBlockRequest.From(
                RequestId.MinRequestId, ClientId.MinClientId, handle, blockId);

            socket.Send(request.Span);

            var response = new CommitBlockResponse(new byte[CommitBlockResponse.SizeInBytes]);
            socket.Receive(response.Span);

            if (response.Status != CommitBlockStatus.Success)
                throw new InvalidOperationException("Failed to commit block.");
        }
    }
}