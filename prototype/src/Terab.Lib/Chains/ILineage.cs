// Copyright Lokad 2018 under MIT BCH.
using System;
using Terab.Lib.Coins;

namespace Terab.Lib.Chains
{
    /// <summary>
    /// This interface provides predicates on <see cref="CoinEvent"/>s.
    /// Its implementation generally relies on information extracted from
    /// <see cref="SimpleBlockchain"/>.
    /// </summary>
    public interface ILineage
    {
        /// <summary>
        /// Return true if <paramref name="block"/> is uncommitted.
        /// </summary>
        /// <remarks>
        /// Method used to validate context block of a write Txo.
        /// </remarks>
        bool IsUncommitted(BlockAlias block);

        /// <summary>
        /// Verify consistency of an event that caller would like to add
        /// with regard to a set of existing events.
        /// </summary>
        bool IsAddConsistent(Span<CoinEvent> events, CoinEvent toAdd);


        /// <summary>
        /// Find among existing events, any production event and any consumption
        /// event consistent with regard to given context.
        /// Returns false if no event was found.
        /// </summary>
        bool TryGetEventsInContext(Span<CoinEvent> events, BlockAlias context, out BlockAlias production,
            out BlockAlias consumption);

        /// <summary>
        /// Indicates whether a coin can be pruned because it has been either
        /// - produced and consumed on the longest chain with enough depth.
        /// - only produced on a now orphaned chain.
        /// </summary>
        bool IsCoinPrunable(Span<CoinEvent> events);
    }
}
