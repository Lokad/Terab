// Copyright Lokad 2018 under MIT BCH.
using Terab.Lib.Messaging;

namespace Terab.Lib.Chains
{
    public enum OpenBlockStatus
    {
        Success,
        ParentNotFound,
    }

    public enum CommitBlockStatus
    {
        Success,
        BlockNotFound,
        BlockIdMismatch,
    }

    public enum GetBlockAliasStatus
    {
        Success,
        BlockNotFound,
    }

    /// <summary>
    /// Abstracts a persistent storage for the blockchain.
    /// </summary>
    public interface IChainStore
    {
        /// <summary> To be called once, prior to any read or write. </summary>
        void Initialize();

        /// <summary> Call is blocking until the newly uncommitted block has been persisted. </summary>
        OpenBlockStatus TryOpenBlock(CommittedBlockId parent, out UncommittedBlock block);

        /// <summary> Call is blocking until the newly committed block has been persisted. </summary>
        CommitBlockStatus TryCommitBlock(BlockAlias alias, CommittedBlockId blockId, out CommittedBlock block);

        GetBlockAliasStatus TryGetAlias(UncommittedBlockId uncommittedBlockId, out BlockAlias alias);

        GetBlockAliasStatus TryGetAlias(CommittedBlockId blockId, out BlockAlias alias);

        bool TryGetCommittedBlock(BlockAlias alias, out CommittedBlock block);

        bool TryGetUncommittedBlock(BlockAlias alias, out UncommittedBlock uncommittedBlock);

        ILineage GetLineage();
    }
}