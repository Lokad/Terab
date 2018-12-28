// Copyright Lokad 2018 under MIT BCH.
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Threading;
using Terab.Lib.Chains;
using Terab.Lib.Coins;
using Terab.Lib.Messaging;
using Terab.Lib.Networking;

namespace Terab.Lib
{
    public class TerabInstance
    {
        private ILog _log;

        public IPAddress IpAddress { get; set; } = IPAddress.Loopback;

        public int Port { get; set; }

        public IChainStore ChainStore { get; set; }

        public ICoinStore[] CoinStores { get; set; }

        public IOutpointHash OutpointHash { get; set; }

        public ChainController ChainController { get; set; }

        public CoinController[] CoinControllers { get; set; }

        public DispatchController DispatchController { get; set; }

        public Listener Listener { get; set; }

        private bool _running;

        private CancellationTokenSource _cancelSource;

        private Thread _chainThread;
        private Thread[] _coinThreads;
        private Thread _dispatchThread;
        private Thread _listenerThread;

        /// <summary>
        /// Creates all the files with their pre-allocated sizes.
        /// </summary>
        public static void InitializeFiles(TerabConfig config, long layer1Capacity, long layer2Capacity = 0,
            ILog log = null)
        {
            log?.Log(LogSeverity.Info, "Starting Terab setup.");

            var sectorCount = layer1Capacity / (Constants.CoinControllerCount * Constants.CoinStoreLayer1SectorSize);
            var coinStoreLayer2SectorSize = (layer2Capacity / layer1Capacity) * Constants.CoinStoreLayer1SectorSize;

            log?.Log(LogSeverity.Info, $"Sector count: {sectorCount}");

            // Queuing checks
            var files = new List<(string path, long fileLength)>();
            var folders = new List<string>();

            // Secret
            var secretPath = Path.Combine(config.Layer1Path, "secret.dat");
            files.Add((secretPath, Constants.SecretStoreFileSize));

            // Chain store
            var chainPath = Path.Combine(config.Layer1Path, "chains.dat");
            files.Add((chainPath, Constants.ChainStoreFileSize));

            // Coin stores
            for (var i = 0; i < Constants.CoinControllerCount; i++)
            {
                var journalPath = Path.Combine(config.Layer1Path, $"coins-journal-{i:x2}.dat");
                files.Add((journalPath, Constants.CoinStoreJournalCapacity));

                var layer1CoinStorePath = Path.Combine(config.Layer1Path, $"coins-1-{i:x2}.dat");
                files.Add((layer1CoinStorePath, sectorCount * Constants.CoinStoreLayer1SectorSize));

                if (!string.IsNullOrEmpty(config.Layer2Path))
                {
                    var layer2CoinStorePath = Path.Combine(config.Layer2Path, $"coins-2-{i:x2}.dat");
                    files.Add((layer2CoinStorePath, sectorCount * coinStoreLayer2SectorSize));
                }

                if (string.IsNullOrEmpty(config.Layer3Path))
                {
                    log?.Log(LogSeverity.Info, $"Layer 3 path isn't defined.");
                }
                else
                {
                    var layer3CoinStorePath = Path.Combine(config.Layer3Path, $"coins-3-{i:x2}.dat");
                    folders.Add(layer3CoinStorePath);
                }
            }

            // Look for conflicts before doing anything
            foreach (var (file, _) in files)
            {
                if (File.Exists(file))
                {
                    log?.Log(LogSeverity.Error, $"Can't initialize, file already exists: {file}.");
                    return;
                }
            }

            foreach (var folder in folders)
            {
                if (Directory.Exists(folder))
                {
                    log?.Log(LogSeverity.Error, $"Can't initialize, folder already exists: {folder}.");
                    return;
                }
            }

            var rollback = false;

            // Once checks are done, proceed
            {
                var currentFile = string.Empty;
                try
                {
                    foreach (var (file, fileLength) in files)
                    {
                        currentFile = file;

                        using (var mmf = MemoryMappedFile.CreateFromFile(currentFile, FileMode.CreateNew, mapName: null,
                            capacity: fileLength))
                        {
                            log?.Log(LogSeverity.Info, $"Created {file} of {fileLength} bytes.");
                        }
                    }
                }
                catch (Exception ex) // most likely failed because of disk full
                {
                    log?.Log(LogSeverity.Error, $"Can't create file {currentFile}: {ex}.");
                    rollback = true;
                }
            }

            {
                var currentFolder = string.Empty;
                try
                {
                    foreach (var folder in folders)
                    {
                        currentFolder = folder;
                        Directory.CreateDirectory(folder);
                        using (var lmdb = new LightningStore<uint>(currentFolder, "coins"))
                        {
                            lmdb.RoundTrip();
                        }
                    }
                }
                catch (Exception ex) // most likely failed because of disk permission
                {
                    log?.Log(LogSeverity.Error, $"Can't create folder {currentFolder}: {ex}.");
                    rollback = true;
                }
            }

            // Error(s) have been encountered, rolling back.
            if (rollback)
            {
                log?.Log(LogSeverity.Error, "Rolling back.");

                foreach (var (file, _) in files)
                {
                    File.Delete(file);
                }

                log?.Log(LogSeverity.Error, "Roll back completed.");

                return;
            }

            log?.Log(LogSeverity.Info, "Terab file creation completed successfully.");
        }

        public TerabInstance(ILog log = null)
        {
            _log = log;
        }

        public void SetupNetwork(TerabConfig config)
        {
            IpAddress = IPAddress.Parse(config.IpAddress);
            Port = config.Port;
        }

        public void SetupStores(TerabConfig config)
        {
            var secretPath = Path.Combine(config.Layer1Path, "secret.dat");
            var secretStore = new MemoryMappedFileSlim(secretPath);
            var secretSpan = secretStore.GetSpan(0, Constants.SecretStoreFileSize);

            // HACK: secret initialization (would be better isolated)
            if (secretSpan.ToArray().All(b => b == 0))
            {
                var rng = RandomNumberGenerator.Create();
                rng.GetBytes(secretSpan);
            }

            OutpointHash = new SipHash(secretSpan);

            var chainPath = Path.Combine(config.Layer1Path, "chains.dat");
            ChainStore = new ChainStore(new MemoryMappedFileSlim(chainPath), _log);
            ChainStore.Initialize();

            CoinStores = new ICoinStore[Constants.CoinControllerCount];
            for (var i = 0; i < Constants.CoinControllerCount; i++)
            {
                var journalPath = Path.Combine(config.Layer1Path, $"coins-journal-{i:x2}.dat");
                var journalFile = new MemoryMappedFileSlim(journalPath);

                var layer1CoinStorePath = Path.Combine(config.Layer1Path, $"coins-1-{i:x2}.dat");
                var layer1File = new MemoryMappedFileSlim(layer1CoinStorePath);

                var sectorCount = (int) (layer1File.FileLength / Constants.CoinStoreLayer1SectorSize);

                // Last layer used for key-value store.
                IKeyValueStore<uint> kvStore = null;
                if (!string.IsNullOrEmpty(config.Layer3Path))
                {
                    var layer3CoinStorePath = Path.Combine(config.Layer3Path, $"coins-3-{i:x2}.dat");
                    kvStore = new LightningStore<uint>(layer3CoinStorePath, "coins");
                }

                IPackStore packStore;
                if (string.IsNullOrEmpty(config.Layer2Path))
                {
                    // Layer 2 is omitted
                    packStore = new PackStore(
                        sectorCount,
                        new[] {Constants.CoinStoreLayer1SectorSize},
                        new[] {layer1File},
                        journalFile,
                        kvStore,
                        _log);
                }
                else
                {
                    // Layer 2 is included
                    var layer2CoinStorePath = Path.Combine(config.Layer2Path, $"coins-2-{i:x2}.dat");
                    var layer2File = new MemoryMappedFileSlim(layer2CoinStorePath);

                    // Sector size on layer 2 is inferred from the sector count on layer 1.
                    if (layer2File.FileLength % sectorCount != 0)
                        throw new InvalidOperationException("Mismatch between sector counts across layers.");

                    var coinStoreLayer1SectorSize = (int) (layer2File.FileLength / sectorCount);

                    packStore = new PackStore(
                        sectorCount,
                        new[] {Constants.CoinStoreLayer1SectorSize, coinStoreLayer1SectorSize},
                        new[] {layer1File, layer2File},
                        journalFile,
                        kvStore,
                        _log);
                }

                packStore.Initialize();

                CoinStores[i] = new SozuTable(packStore, OutpointHash);
            }

            _log?.Log(LogSeverity.Info, "Terab store initialization completed successfully.");
        }

        public void SetupControllers()
        {
            // == Setup inboxes ==
            var dispatchInbox = new BoundedInbox(Constants.DispatchControllerOutboxSize);
            var coinInboxes = new BoundedInbox[CoinStores.Length];
            for (var i = 0; i < coinInboxes.Length; i++)
                coinInboxes[i] = new BoundedInbox(Constants.CoinControllerInboxSize);
            var chainInbox = new BoundedInbox(Constants.ChainControllerInboxSize);

            // == Setup controllers ==
            Listener = new Listener(
                dispatchInbox,
                IpAddress,
                Port,
                _log);

            // Zero port can be re-associated to an available port.
            Port = Listener.Port; 

            DispatchController = new DispatchController(
                dispatchInbox,
                chainInbox,
                coinInboxes,
                OutpointHash,
                _log);

            CoinControllers = new CoinController[CoinStores.Length];
            for (var i = 0; i < CoinControllers.Length; i++)
                CoinControllers[i] = new CoinController(
                    coinInboxes[i],
                    dispatchInbox,
                    CoinStores[i],
                    OutpointHash,
                    _log,
                    shardIndex: i);

            ChainController = new ChainController(
                ChainStore,
                chainInbox,
                dispatchInbox,
                lineage =>
                {
                    // Whenever 'OpenBlock' or 'CommitBlock' is requested, lineages get refreshed.
                    foreach (var c in CoinControllers)
                        c.Lineage = lineage;
                },
                _log);

            // == Bind controllers ==
            Listener.OnConnectionAccepted = conn => { DispatchController.AddConnection(conn); };

            foreach (var cc in CoinControllers)
                cc.OnRequestHandled = () => { DispatchController.Wake(); };

            ChainController.OnRequestHandled = () => { DispatchController.Wake(); };

            DispatchController.OnConnectionAccepted = conn => conn.Start();
            DispatchController.OnBlockMessageDispatched = () => { ChainController.Wake(); };

            for (var i = 0; i < CoinStores.Length; i++)
            {
                var coinController = CoinControllers[i];
                DispatchController.OnCoinMessageDispatched[i] = () => { coinController.Wake(); };
            }
        }

        public void Start()
        {
            if(_running)
                throw new InvalidOperationException("Instance is already running.");

            _running = true;

            _cancelSource = new CancellationTokenSource();

            var cancel = _cancelSource.Token;

            _chainThread = new Thread(() => ChainController.Loop(cancel))
                { Name = "ChainController", IsBackground = true };

            _coinThreads = new Thread[CoinControllers.Length];
            for (var i = 0; i < CoinControllers.Length; i++)
            {
                var coinController = CoinControllers[i];
                _coinThreads[i] = new Thread(() => coinController.Loop(cancel))
                    { Name = $"CoinController[{i}]", IsBackground = true };
            }

            _dispatchThread = new Thread(() => DispatchController.Loop(cancel))
                { Name = "DispatchController", IsBackground = true };

            _listenerThread = new Thread(() => Listener.Loop(cancel))
                { Name = "Listener", IsBackground = true };

            _chainThread.Start();
            foreach (var t in _coinThreads)
                t.Start();
            _dispatchThread.Start();
            _listenerThread.Start();
        }

        public void Stop()
        {
            _cancelSource.Cancel();

            // Wake controllers to let them terminate.
            ChainController.Wake();
            foreach(var c in CoinControllers)
                c.Wake();
            DispatchController.Wake();

            _chainThread.Join();
            foreach (var t in _coinThreads)
                t.Join();
            _dispatchThread.Join();
            _listenerThread.Join();

            _running = false;
        }

        private static byte[] StringToByteArray(string hex)
        {
            return Enumerable.Range(0, hex.Length)
                .Where(x => x % 2 == 0)
                .Select(x => Convert.ToByte(hex.Substring(x, 2), 16))
                .ToArray();
        }
    }
}