// Copyright Lokad 2018 under MIT BCH.
using System.Linq;
using Terab.Lib.Chains;
using Terab.Lib.Coins;
using Terab.Lib.Messaging;
using Xunit;

namespace Terab.Lib.Tests.Coins
{
    public unsafe class CoinTests
    {
        [Fact]
        public void GetSetOutpoint()
        {
            var buffer = new byte[4096];
            var coin = new Coin(buffer);

            Assert.Equal((Outpoint)default, coin.Outpoint);

            var outpoint = new Outpoint();
            for (var i = 0; i < 32; i++)
                outpoint.TxId[i] = (byte) i;
            outpoint.TxIndex = 42;

            coin.SetOutpoint(ref outpoint);

            Assert.Equal(outpoint, coin.Outpoint);
        }

        [Fact]
        public void GetSetOutpointFlags()
        {
            var buffer = new byte[4096];
            var coin = new Coin(buffer);

            Assert.Equal((TerabOutpointFlags)default, coin.OutpointFlags);

            var flags = TerabOutpointFlags.PersistentIsCoinbase;

            coin.OutpointFlags = flags;
            Assert.Equal(flags, coin.OutpointFlags);
        }

        [Fact]
        public void AppendEvents()
        {
            var buffer = new byte[4096];
            var coin = new Coin(buffer);

            Assert.Equal(0, coin.Events.Length);

            var events = new[]
            {
                new CoinEvent(new BlockAlias(12, 0), CoinEventKind.Production),
                new CoinEvent(new BlockAlias(14, 2), CoinEventKind.Consumption),
            };

            coin.SetEvents(events);

            Assert.True(events.SequenceEqual(coin.Events.ToArray()));
        }
    }
}
