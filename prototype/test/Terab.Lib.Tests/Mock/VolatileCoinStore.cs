// Copyright Lokad 2018 under MIT BCH.
using System.Collections.Generic;
using Terab.Lib.Chains;
using Terab.Lib.Coins;
using Terab.Lib.Messaging;

namespace Terab.Lib.Tests.Mock
{
    /// <summary>
    /// Memory-only mock storage implementation, intended for tests and benchmarks.
    /// </summary>
    /// <remarks>
    /// Lineages are IGNORED by this mock implementation.
    /// </remarks>
    public class VolatileCoinStore : ICoinStore
    {
        private class FatCoin
        {
            readonly byte[] _coinBuffer;

            public List<CoinEvent> Events { get; }

            public Coin Coin => new Coin(_coinBuffer);

            public FatCoin(Coin coin, CoinEvent production)
            {
                _coinBuffer = new byte[coin.Span.Length];
                coin.Span.CopyTo(_coinBuffer);
                Events = new List<CoinEvent> {production};
            }
        }

        private unsafe class OutpointComparer : IEqualityComparer<Outpoint>
        {
            public bool Equals(Outpoint x, Outpoint y)
            {
                return x.Equals(y);
            }

            public int GetHashCode(Outpoint obj)
            {
                var hash = 0;
                for (var i = 0; i < 4; i++)
                    hash += obj.TxId[i] << (i * 8);

                return hash ^ obj.TxIndex;
            }
        }

        private SpanPool<byte> _pool;

        private Dictionary<Outpoint, FatCoin> _coins;

        public VolatileCoinStore()
        {
            _pool = new SpanPool<byte>(65536);
            _coins = new Dictionary<Outpoint, FatCoin>(new OutpointComparer());
        }

        public bool TryGet(ulong outpointHash, ref Outpoint outpoint, BlockAlias context, ILineage lineage, out Coin coin,
            out BlockAlias production, out BlockAlias consumption)
        {
            production = BlockAlias.Undefined;
            consumption = BlockAlias.Undefined;

            if (_coins.TryGetValue(outpoint, out var fatCoin))
            {
                foreach(var ev in fatCoin.Events)
                {
                    if (ev.Kind == CoinEventKind.Production)
                        production = ev.BlockAlias;

                    if (ev.Kind == CoinEventKind.Consumption)
                        consumption = ev.BlockAlias;
                }

                if (lineage == null || lineage.TryGetEventsInContext(fatCoin.Events.ToArray(), context, out production, out consumption))
                {
                    coin = fatCoin.Coin;

                    return true;
                }
            }

            coin = Coin.Empty;
            return false;
        }

        public CoinChangeStatus AddProduction(ulong outpointHash, ref Outpoint outpoint, bool isCoinBase, Payload payload,
            BlockAlias context, ILineage lineage)
        {
            var production = new CoinEvent(context, CoinEventKind.Production);

            if (_coins.TryGetValue(outpoint, out var fatCoin))
            {
                if (fatCoin.Events.Contains(production))
                    return CoinChangeStatus.Success; // done

                fatCoin.Events.Add(production);

                return CoinChangeStatus.Success;
            }

            var coin = new Coin(ref outpoint, isCoinBase, payload, production, _pool);
            _coins.Add(outpoint, new FatCoin(coin, production));

            return CoinChangeStatus.Success;
        }

        public CoinChangeStatus AddConsumption(ulong outpointHash, ref Outpoint outpoint, BlockAlias context,
            ILineage lineage)
        {
            if (!_coins.TryGetValue(outpoint, out var fatCoin))
                return CoinChangeStatus.OutpointNotFound;

            var consumption = new CoinEvent(context, CoinEventKind.Consumption);

            if (fatCoin.Events.Contains(consumption))
                return CoinChangeStatus.Success;

            fatCoin.Events.Add(consumption);
            return CoinChangeStatus.Success;
        }

        public CoinChangeStatus Remove(ulong outpointHash, ref Outpoint outpoint, BlockAlias context, CoinRemoveOption option,
            ILineage lineage)
        {
            if (!_coins.TryGetValue(outpoint, out var fatCoin))
                return CoinChangeStatus.OutpointNotFound;

            fatCoin.Events.RemoveAll(ev => ev.BlockAlias == context &&
                                           (((option & CoinRemoveOption.RemoveProduction) != 0 &&
                                            ev.Kind == CoinEventKind.Production) ||
                                            ((option & CoinRemoveOption.RemoveConsumption) != 0 &&
                                             ev.Kind == CoinEventKind.Consumption)));

            return CoinChangeStatus.Success;
        }
    }
}
