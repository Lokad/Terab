// Copyright Lokad 2018 under MIT BCH.
using System;
using Terab.Lib.Coins;
using Terab.Lib.Messaging;
using Terab.Lib.Messaging.NativeClient;
using Terab.Lib.Tests.Mock;
using Xunit;
using Xunit.Abstractions;
using Coin = Terab.Lib.Messaging.NativeClient.Coin;

namespace Terab.Lib.Tests.Messaging.NativeClient
{
    public class TerabClientTests
    {
        private readonly ILog _log;

        public TerabClientTests(ITestOutputHelper output)
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

        static unsafe Outpoint GetOutpoint(int seed)
        {
            var outpoint = new Outpoint();

            for (var i = 0; i < 4; i++)
                outpoint.TxId[i] = (byte) (seed >> (i * 8));

            outpoint.TxIndex = seed;

            return outpoint;
        }

        public (Coin[] coins, byte[] storage) GetProductionCoins(BlockHandle handle)
        {
            var coins = new Coin[3];

            coins[0] = new Coin
            {
                Outpoint = GetOutpoint(123),
                Production = handle,
                Consumption = BlockHandle.Undefined,
                Satoshis = 42,
                NLockTime = 11,
                ScriptOffset = 0,
                ScriptLength = 10,
                Flags = CoinFlags.Coinbase,
                Status = CoinStatus.None,
            };

            coins[1] = new Coin
            {
                Outpoint = GetOutpoint(124),
                Production = handle,
                Consumption = BlockHandle.Undefined,
                Satoshis = 43,
                NLockTime = 12,
                ScriptOffset = 10,
                ScriptLength = 20,
                Flags = CoinFlags.None,
                Status = CoinStatus.None,
            };

            coins[2] = new Coin
            {
                Outpoint = GetOutpoint(125),
                Production = handle,
                Consumption = BlockHandle.Undefined,
                Satoshis = 44,
                NLockTime = 13,
                ScriptOffset = 30,
                ScriptLength = 40,
                Flags = CoinFlags.None,
                Status = CoinStatus.None,
            };

            var storage = new byte[70];
            for (var i = 0; i < storage.Length; i++)
                storage[i] = (byte)i;

            return (coins, storage);
        }


        [Fact]
        public void Connect()
        {
            var instance = GetInstance();

            TerabClient client = null;

            try
            {
                instance.Start();
                client = new TerabClient($"127.0.0.1:{instance.Port}");

                var handle = client.OpenBlock(CommittedBlockId.GenesisParent, out var ucid);

                var handleBis = client.GetUncommittedBlock(ucid);
                Assert.True(handle.Equals(handleBis));

                var blockInfo = client.GetBlockInfo(handle);
                Assert.False(blockInfo.Flags.Equals(BlockFlags.Committed));

                var (coins, storage) = GetProductionCoins(handle);

                client.SetCoins(handle, coins, storage);

                client.CommitBlock(handle, CommittedBlockId.Genesis);

                // Retrieve coins (bis)
                var coinsBis = new Coin[3];
                var storageBis = new byte[1024];
                for (var i = 0; i < coins.Length; i++) coinsBis[i].Outpoint = coins[i].Outpoint;

                client.GetCoins(handle, coinsBis, storageBis);

                for (var i = 0; i < coins.Length; i++)
                {
                    Assert.True(coins[i].Outpoint.Equals(coinsBis[i].Outpoint));
                    Assert.True(coins[i].Production.Equals(coinsBis[i].Production));
                    Assert.True(coins[i].Consumption.Equals(coinsBis[i].Consumption));
                    Assert.True(coins[i].Satoshis.Equals(coinsBis[i].Satoshis));
                    Assert.True(coins[i].NLockTime.Equals(coinsBis[i].NLockTime));
                    Assert.True(coins[i].Flags.Equals(coinsBis[i].Flags));

                    Assert.True(
                        new Span<byte>(storage).Slice(coins[i].ScriptOffset, coins[i].ScriptLength).SequenceEqual(
                            new Span<byte>(storageBis).Slice(coinsBis[i].ScriptOffset, coinsBis[i].ScriptLength)));

                    Assert.True((coinsBis[i].Status & CoinStatus.StorageTooShort) == 0);
                }

                var handleTer = client.GetCommittedBlock(CommittedBlockId.Genesis);
                Assert.True(handle.Equals(handleTer));

                // Retrieve coins (ter)
                var coinsTer = new Coin[3];
                var storageTer = new byte[1]; // too short, on purpose
                for (var i = 0; i < coins.Length; i++) coinsTer[i].Outpoint = coins[i].Outpoint;

                client.GetCoins(handle, coinsTer, storageTer);

                for (var i = 0; i < coins.Length; i++)
                {
                    Assert.True(coins[i].Outpoint.Equals(coinsTer[i].Outpoint));
                    Assert.True(coins[i].Production.Equals(coinsTer[i].Production));
                    Assert.True(coins[i].Consumption.Equals(coinsTer[i].Consumption));
                    Assert.True(coins[i].Satoshis.Equals(coinsTer[i].Satoshis));
                    Assert.True(coins[i].NLockTime.Equals(coinsTer[i].NLockTime));
                    Assert.True(coins[i].Flags.Equals(coinsTer[i].Flags));

                    Assert.True((coinsTer[i].Status & CoinStatus.StorageTooShort) != 0);
                }

                var blockInfoBis = client.GetBlockInfo(handle);
                Assert.True(blockInfoBis.Flags.Equals(BlockFlags.Committed));

                Assert.True(blockInfoBis.BlockId.Equals(CommittedBlockId.Genesis));
            }
            finally
            {
                client?.Dispose();
                instance.Stop();
            }
        }
    }
}
