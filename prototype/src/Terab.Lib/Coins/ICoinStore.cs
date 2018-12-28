// Copyright Lokad 2018 under MIT BCH.
using System;
using Terab.Lib.Chains;
using Terab.Lib.Messaging;

namespace Terab.Lib.Coins
{
    /// <summary>
    /// Return code of change methods of <see cref="ICoinStore"/>.
    /// </summary>
    public enum CoinChangeStatus
    {
        Success,
        InvalidContext,
        OutpointNotFound,
        InvalidBlockHandle
    }

    /// <summary>
    /// Clarify the semantic of the removal request.
    /// </summary>
    [Flags]
    public enum CoinRemoveOption
    {
        None = 0,
        RemoveProduction = 1 << 0,
        RemoveConsumption = 1 << 1,
    }

    /// <summary>
    /// A blockchain-centric access to the coin dataset.
    /// </summary>
    public interface ICoinStore
    {
        /// <summary> Returns 'true' if outpoint is found, 'false' otherwise. </summary>
        /// <param name="outpointHash"> DOS-resilient hash. </param>
        /// <param name="outpoint"> Target of the query. </param>
        /// <param name="context"> Context block of get request. </param>
        /// <param name="lineage"> Lineage derived from blockchain</param>
        /// <param name="coin"> Populated only if 'get' succeed. </param>
        /// <param name="production">
        /// Production block of outpoint with respect to
        /// <paramref name="context"/> and <paramref name="lineage"/>
        /// </param>
        /// <param name="consumption">
        /// Consumption block of outpoint with respect to
        /// <paramref name="context"/> and <paramref name="lineage"/>
        /// </param>
        bool TryGet(ulong outpointHash, ref Outpoint outpoint, BlockAlias context, ILineage lineage,
            out Coin coin, out BlockAlias production, out BlockAlias consumption);

        CoinChangeStatus AddProduction(ulong outpointHash, 
            ref Outpoint outpoint, 
            bool isCoinBase,
            Payload payload,
            BlockAlias context, ILineage lineage);

        CoinChangeStatus AddConsumption(ulong outpointHash, ref Outpoint outpoint, BlockAlias context,
            ILineage lineage);

        CoinChangeStatus Remove(ulong outpointHash, ref Outpoint outpoint, BlockAlias context,
            CoinRemoveOption option,
            ILineage lineage);
    }
}
