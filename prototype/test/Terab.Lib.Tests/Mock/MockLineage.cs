// Copyright Lokad 2018 under MIT BCH.
using System;
using Terab.Lib.Chains;
using Terab.Lib.Coins;

namespace Terab.Lib.Tests.Mock
{
    public class MockLineage : ILineage
    {
        public static CoinEvent CoinEventToPrune = new CoinEvent(new BlockAlias(10, 0), CoinEventKind.Production);

        private readonly Func<bool> _addConsistentFunc;

        private readonly ReadBlockAlias _readProduction;

        private readonly ReadBlockAlias _readConsumption;

        public delegate void ReadBlockAlias(out BlockAlias result);

        public static ReadBlockAlias Undefined => (out BlockAlias result) => result = BlockAlias.Undefined;

        public bool PruningActive { get; set; }


        public MockLineage(Func<bool> addConsistentFunc, ReadBlockAlias readProduction,
            ReadBlockAlias readConsumption)
        {
            _addConsistentFunc = addConsistentFunc;
            _readProduction = readProduction;
            _readConsumption = readConsumption;
            PruningActive = false;
        }

        public MockLineage() : this(() => true, Undefined, Undefined)
        {
        }

        public MockLineage(Func<bool> addConsistentFunc) : this(addConsistentFunc, Undefined, Undefined)
        {
        }

        public MockLineage(ReadBlockAlias readProduction, ReadBlockAlias readConsumption) : this(() => true,
            readProduction, readConsumption)
        {
        }

        public bool IsUncommitted(BlockAlias block)
        {
            return true;
        }

        public bool IsAddConsistent(Span<CoinEvent> events, CoinEvent toAdd)
        {
            return _addConsistentFunc.Invoke();
        }

        public bool TryGetEventsInContext(Span<CoinEvent> events, BlockAlias context, out BlockAlias production,
            out BlockAlias consumption)
        {
            _readProduction(out production);
            _readConsumption(out consumption);
            return true;
        }

        public bool IsAncestor(BlockAlias subject, BlockAlias maybeAncestor)
        {
            return maybeAncestor.BlockHeight < subject.BlockHeight;
        }

        public bool IsBlockPrunable(BlockAlias block)
        {
            return false;
        }

        public bool IsCoinPrunable(Span<CoinEvent> events)
        {
            return PruningActive && events.Length == 1 && events[0].Equals(CoinEventToPrune);
        }

    }
}
