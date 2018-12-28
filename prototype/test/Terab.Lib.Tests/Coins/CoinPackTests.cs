// Copyright Lokad 2018 under MIT BCH.
using System;
using Terab.Lib.Chains;
using Terab.Lib.Coins;
using Terab.Lib.Messaging;
using Xunit;

namespace Terab.Lib.Tests.Coins
{
    public unsafe class CoinPackTests
    {
        private Span<OutpointSig> GetSigs(Random rand)
        {
            var sigs = new OutpointSig[(byte)rand.Next()];
            for (var i = 0; i < sigs.Length; i++)
                sigs[i] = new OutpointSig((ushort)rand.Next());

            return sigs;
        }

        private static Coin GetCoin(Random rand)
        {
           return GetCoin(rand, (byte)rand.Next());
        }

        private static Coin GetCoin(Random rand, int scriptLength)
        {
            var coin = new Coin(new byte[4096]);

            var outpoint = new Outpoint();
            for (var i = 0; i < 32; i++)
                outpoint.TxId[i] = (byte)rand.Next();
            outpoint.TxIndex = rand.Next();

            coin.Outpoint = outpoint;

            var events = new[] { new CoinEvent(new BlockAlias(123, 0), CoinEventKind.Production) };
            coin.SetEvents(events);

            var script = new byte[scriptLength];
            rand.NextBytes(script);

            var payload = new Payload(new byte[4096]);
            payload.NLockTime = (uint)rand.Next();
            payload.Satoshis = (ulong)rand.Next();
            payload.Append(script);

            coin.SetPayload(payload);

            return coin;
        }

        [Fact]
        public void Empty()
        {
            var pack = new CoinPack(new byte[4096]);
            Assert.Equal(0, pack.CoinCount);
        }

        [Fact]
        public void GetSetSectorProperties()
        {
            var pack = new CoinPack(new byte[4096]);
            Assert.Equal(0u, pack.SectorIndex);
            Assert.Equal(0, pack.LayerIndex);
            Assert.Equal(0, pack.WriteColor);
            Assert.Equal(0, pack.WriteCount);

            pack.SectorIndex = 42;
            pack.LayerIndex = 7;
            pack.WriteColor = 1;
            pack.WriteCount = 3;
            Assert.Equal(42u, pack.SectorIndex);
            Assert.Equal(7, pack.LayerIndex);
            Assert.Equal(1, pack.WriteColor);
            Assert.Equal(3, pack.WriteCount);
        }

        [Fact]
        public void GetSetOutpointSigs()
        {
            var rand = new Random(42);

            var pack = new CoinPack(new byte[4096]);
            var sigs = GetSigs(rand);
            pack.SetOutpointSigs(sigs);

            Assert.Equal(sigs.Length, pack.OutpointSigCount);

            var shallow = pack.OutpointSigs;
            Assert.True(shallow.SequenceEqual(sigs));
        }

        [Fact]
        public void CoinCountAfterAppend()
        {
            var pack = new CoinPack(new byte[4096]);
            var coin = new Coin(new byte[128]);

            pack.Append(coin);
            Assert.Equal(1, pack.CoinCount);

            pack.Append(coin);
            Assert.Equal(2, pack.CoinCount);
        }

        [Fact]
        public void AppendCoin()
        {
            var rand = new Random(42);

            var pack = new CoinPack(new byte[4096]);
            pack.SetOutpointSigs(GetSigs(rand));

            var coin1 = GetCoin(rand);
            var coin2 = GetCoin(rand);
            var coin3 = GetCoin(rand);

            pack.Append(coin1);
            Assert.Equal(1, pack.CoinCount);

            pack.Append(coin2);
            Assert.Equal(2, pack.CoinCount);

            pack.Append(coin3);
            Assert.Equal(3, pack.CoinCount);

            pack.TryGet(ref coin1.Outpoint, out Coin g1);
            Assert.True(coin1.Span.SequenceEqual(g1.Span));
            
            pack.TryGet(ref coin2.Outpoint, out Coin g2);
            Assert.True(coin2.Span.SequenceEqual(g2.Span));
            
            pack.TryGet(ref coin3.Outpoint, out Coin g3);
            Assert.True(coin3.Span.SequenceEqual(g3.Span));

            // should not be found
            var coin4 = GetCoin(rand);
            pack.TryGet(ref coin4.Outpoint, out Coin g4);
            Assert.True(g4.IsEmpty);
        }

        [Fact]
        public void GetOnEmptyCoinPack()
        {
            var rand = new Random(42);

            var pack = new CoinPack(new byte[4096]);
            var coin = GetCoin(rand);
            Assert.False(pack.TryGet(ref coin.Outpoint, out Coin g1));
            Assert.True(g1.IsEmpty);
        }

        [Fact]
        public void GetSpan()
        {
            var rand = new Random(42);

            var pack = new CoinPack(new byte[4096]);
            pack.SetOutpointSigs(GetSigs(rand));

            var coin1 = GetCoin(rand);
            var coin2 = GetCoin(rand);
            pack.Append(coin1);
            pack.Append(coin2);

            var shallow = new CoinPack(pack.Span);

            shallow.TryGet(ref coin2.Outpoint, out Coin g2);
            Assert.True(coin2.Span.SequenceEqual(g2.Span));
        }

        [Fact]
        public void ResetAndGetSpan()
        {
            var rand = new Random(42);

            var pack = new CoinPack(new byte[4096]);
            pack.SetOutpointSigs(GetSigs(rand));
            pack.Append(GetCoin(rand));
            pack.Append(GetCoin(rand));

            // reset
            pack.Reset();

            var coin1 = GetCoin(rand);
            var coin2 = GetCoin(rand);
            pack.Append(coin1);
            pack.Append(coin2);

            var shallow = new CoinPack(pack.Span);

            shallow.TryGet(ref coin2.Outpoint, out Coin g2);
            Assert.True(coin2.Span.SequenceEqual(g2.Span));
        }

        [Fact]
        public void AppendManyZeroOffset()
        {
            var rand = new Random(42);

            var pack1 = new CoinPack(new byte[4096]);
            pack1.SetOutpointSigs(GetSigs(rand));

            var coin1 = GetCoin(rand);
            var coin2 = GetCoin(rand);
            var coin3 = GetCoin(rand);

            pack1.Append(coin1);
            pack1.Append(coin2);
            pack1.Append(coin3);

            var pack2 = new CoinPack(new byte[4096]);
            pack2.Append(pack1, 0, 2);

            Assert.Equal(2, pack2.CoinCount);

            pack2.TryGet(ref coin1.Outpoint, out Coin g1);
            Assert.True(coin1.Span.SequenceEqual(g1.Span));

            pack2.TryGet(ref coin2.Outpoint, out Coin g2);
            Assert.True(coin2.Span.SequenceEqual(g2.Span));

            pack2.TryGet(ref coin3.Outpoint, out Coin g3);
            Assert.True(g3.IsEmpty);
        }

        [Fact]
        public void AppendManyWithOffset()
        {
            var rand = new Random(42);

            var pack1 = new CoinPack(new byte[4096]);
            pack1.SetOutpointSigs(GetSigs(rand));

            var coin1 = GetCoin(rand);
            var coin2 = GetCoin(rand);
            var coin3 = GetCoin(rand);

            pack1.Append(coin1);
            pack1.Append(coin2);
            pack1.Append(coin3);

            var pack2 = new CoinPack(new byte[4096]);
            pack2.Append(pack1, 1, 2);

            Assert.Equal(2, pack2.CoinCount);

            pack2.TryGet(ref coin1.Outpoint, out Coin g1);
            Assert.True(g1.IsEmpty);

            pack2.TryGet(ref coin2.Outpoint, out Coin g2);
            Assert.True(coin2.Span.SequenceEqual(g2.Span));

            pack2.TryGet(ref coin3.Outpoint, out Coin g3);
            Assert.True(coin3.Span.SequenceEqual(g3.Span));
        }

        [Fact]
        public void CountCoinsAbove()
        {
            var rand = new Random(42);

            var pack = new CoinPack(new byte[4096]);
            pack.SetOutpointSigs(GetSigs(rand));

            for(var i = 0; i < 10; i++)
                pack.Append(GetCoin(rand));

            Assert.Equal(1, pack.CountCoinsAbove(10));
            Assert.Equal(3, pack.CountCoinsAbove(500));
            Assert.Equal(10, pack.CountCoinsAbove(10000));
        }
    }
}
