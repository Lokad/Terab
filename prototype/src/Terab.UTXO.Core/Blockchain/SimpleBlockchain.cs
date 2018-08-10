using System;
using System.Collections.Generic;
using System.Linq;
using Terab.UTXO.Core.Hash;
using Terab.UTXO.Core.Serializer;

namespace Terab.UTXO.Core.Blockchain
{
    /// <summary>
    /// Represents a blockchain. Adding blocks yields block aliases which
    /// can be used to retrieve the metadata of a block such identified.
    /// </summary>
    /// <seealso cref="BlockchainSerializer"/>
    /// <seealso cref="BlockchainStrategies"/>
    /// <seealso cref="OptimizedLineage"/>
    public class SimpleBlockchain
    {
        /// <summary>
        /// List of all committed blocks on the blockchain. Parents of committed
        /// blocks have to be committed, too.
        /// </summary>
        /// <remarks>
        /// As the first block is special in the sense that it can be added
        /// without having a parent,
        /// it has to be added via <see cref="OpenFirstBlock"/>.
        /// </remarks>
        private readonly List<CommittedBlock> _committed;

        /// <summary>
        /// List of all uncommitted blocks in the blockchain.
        /// </summary>
        private readonly List<UncommittedBlock> _uncommitted;

        /// <summary>
        /// Maximum blockheight of a block in the blockchain.
        /// </summary>
        public int BlockchainHeight { get; private set; }

        /// <summary>
        /// Create an empty blockchain object that has to be filled
        /// block by block.
        /// </summary>
        public SimpleBlockchain()
        {
            _committed = new List<CommittedBlock>();
            _uncommitted = new List<UncommittedBlock>();
        }

        /// <summary>
        /// Create a blockchain object with the list of blocks given.
        /// </summary>
        /// <remarks>
        /// This constructor can be used together with <see cref="BlockchainSerializer.ReadFrom"/>.
        /// </remarks>
        public SimpleBlockchain(List<CommittedBlock> committed, List<UncommittedBlock> uncommitted,
            int blockchainHeight)
        {
            _committed = committed;
            _uncommitted = uncommitted;
            BlockchainHeight = blockchainHeight;
        }

        /// <summary>
        /// Number of elements currently in the blockchain.
        /// </summary>
        public int BlockchainLength => _committed.Count + _uncommitted.Count;

        public int CommittedBlockCount => _committed.Count;

        public int UncommittedBlockCount => _uncommitted.Count;

        /// <summary>
        /// Appends a new block to the end of the current committedBlocks.
        /// The first block cannot be added via this method,
        /// <see cref="OpenFirstBlock"/> will have to be used instead.
        /// </summary>
        /// <returns>The alias of the added block.</returns>
        public UncommittedBlock OpenBlock(BlockAlias parentAlias)
        {
            // Verify if block to add is empty
            if (parentAlias == BlockAlias.GenesisParent)
                throw new ArgumentException("Blocks cannot have GenesisParent as parent.");

            var parentIsNotInCBlock = !FindCommitted(block => block.Alias == parentAlias, out var parent);
            // Parent has to be a valid block.

            var parentIsNotInUBlock = !FindUncommitted(block => block.Alias == parentAlias, out var parentUnc);
            if (parentIsNotInCBlock && parentIsNotInUBlock)
            {
                throw new ArgumentException($"{parentAlias} does not exist.");
            }

            // TOOD: [vermorel] Why do we have this small forest of tests? Comment needed
            // TODO: [vermorel] Introduce a Linq-like helper 'QuickLast' to avoid the pattern '_array[_array.Length - 1]'.

            BlockAlias newBlockAlias;
            if (_uncommitted.Count == 0)
                newBlockAlias = _committed[_committed.Count - 1].Alias.GetNext();
            else if (_committed.Count == 0)
                newBlockAlias = _uncommitted[_uncommitted.Count - 1].Alias.GetNext();
            else // a committed block can have a higher alias than a non-committed one if they are on different forked chains
                newBlockAlias = BlockAlias.GetJoinNext(_committed[_committed.Count - 1].Alias,
                    _uncommitted[_uncommitted.Count - 1].Alias);

            var newBlockIdentifier = TempBlockId.Create();
            int newBlockBlockHeight = 1 + (parentIsNotInCBlock ? parentUnc.BlockHeight : parent.BlockHeight);

            var newBlock = new UncommittedBlock(newBlockBlockHeight, newBlockIdentifier, newBlockAlias, parentAlias);

            _uncommitted.Add(newBlock);
            if (newBlockBlockHeight > BlockchainHeight)
                BlockchainHeight = newBlockBlockHeight;

            return newBlock;
        }

        /// <summary>
        /// Commits the uncommitted block referenced by its block alias.
        /// The hash ID of the future committed block has to be given as argument to the
        /// function.
        /// </summary>
        /// <returns></returns>
        public bool CommitBlock(BlockAlias alias, BlockId blockId)
        {
            if (!RetrieveUncommittedBlock(alias, out var toCommit))
                throw new ArgumentException($"ID {alias} does not exist or is already committed.");

            // verify that parent of block to commit is committed itself
            if (!RetrieveCommittedBlock(toCommit.Parent, out _) && !blockId.Equals(BlockId.Genesis))
                throw new ArgumentException($"Parent {toCommit.Parent} is not yet committed.");

            var blockToCommit = toCommit;
            _uncommitted.Remove(toCommit);

            var newBlock = new CommittedBlock(blockToCommit.BlockHeight, blockId, blockToCommit.Alias,
                blockToCommit.Parent);
            _committed.Add(newBlock);

            return true;
        }

        /// <summary>
        /// Starts a new blockchain with one block.
        /// All data possibly in the committedBlocks
        /// before the call of this method is going to be deleted.
        /// </summary>
        /// <returns>The alias of the added block.</returns>
        public UncommittedBlock OpenFirstBlock()
        {
            _committed.Clear();
            _uncommitted.Clear();

            var newBlockIdentifier = TempBlockId.Create();

            var newBlock = new UncommittedBlock(0, newBlockIdentifier, BlockAlias.Genesis, BlockAlias.GenesisParent);
            _uncommitted.Add(newBlock);

            return newBlock;
        }

        /// <summary>
        /// Returns the block alias of the committed block
        /// identified by the ID given as hash in the argument.
        /// </summary>
        public BlockAlias RetrieveAlias(BlockId blockId)
        {
            if (FindCommitted(b => b.BlockId.Equals(blockId), out var block))
                return block.Alias;

            return BlockAlias.Undefined;
        }

        /// <summary>
        /// Returns the block alias of the uncommitted block
        /// identified by the ID given in the argument.
        /// </summary>
        public BlockAlias RetrieveAlias(TempBlockId blockId)
        {
            if (FindUncommitted(b => b.BlockId.Equals(blockId), out var block))
                return block.Alias;

            return BlockAlias.Undefined;
        }

        /// <summary>
        /// Returns the block metadata of the committed block
        /// identified by the alias given as argument.
        /// </summary>
        public bool RetrieveCommittedBlock(BlockAlias alias, out CommittedBlock block)
        {
            return FindCommitted(b => b.Alias == alias, out block);
        }

        /// <summary>
        /// Returns the block metadata of the uncommitted block
        /// identified by the alias given as argument.
        /// </summary>
        public bool RetrieveUncommittedBlock(BlockAlias alias, out UncommittedBlock block)
        {
            return FindUncommitted(b => b.Alias == alias, out block);
        }

        /// <summary>
        /// Deletes the block from the blockchain identified
        /// by the alias given as argument, if it exists.
        /// No conditions are attached to a deletion.
        /// The calling party has to make sure the blockchain
        /// does not break by deleting the block.
        /// </summary>
        public bool DeleteBlock(BlockAlias alias)
        {
            if (RetrieveUncommittedBlock(alias, out var blockU))
            {
                _uncommitted.Remove(blockU);
                RecalculateBlockchainHeight();
                return true;
            }

            if (RetrieveCommittedBlock(alias, out var blockC))
            {
                _committed.Remove(blockC);
                RecalculateBlockchainHeight();
                return true;
            }

            return false;
        }

        private void RecalculateBlockchainHeight()
        {
            // TODO: [vermorel] Introduce a Linq-like help named 'MaxOrZeroIfEmpty()'

            if (_uncommitted.Count == 0)
            {
                BlockchainHeight = _committed.Max(o => o.BlockHeight);
            }
            else if (_committed.Count == 0)
            {
                BlockchainHeight = _uncommitted.Max(o => o.BlockHeight);
            }
            else
            {
                BlockchainHeight = Math.Max(
                    _committed.Max(o => o.BlockHeight),
                    _uncommitted.Max(o => o.BlockHeight));
            }
        }

        private bool FindCommitted(Predicate<CommittedBlock> p, out CommittedBlock foundBlock)
        {
            foundBlock = _committed.FindLast(p);
            return !foundBlock.Equals(default(CommittedBlock));
        }

        private bool FindUncommitted(Predicate<UncommittedBlock> p, out UncommittedBlock foundBlock)
        {
            foundBlock = _uncommitted.FindLast(p);
            return !foundBlock.Equals(default(UncommittedBlock));
        }

        /// <summary>
        /// Enumerates all committed blocks from the beginning.
        /// </summary>
        public IEnumerable<CommittedBlock> GetCommitted()
        {
            return _committed.AsEnumerable();
        }

        /// <summary>
        /// Enumerates all committed blocks from the end.
        /// </summary>
        public IEnumerable<CommittedBlock> GetReverseCommitted()
        {
            return GetCommitted().Reverse();
        }

        /// <summary>
        /// Enumerates all uncommitted blocks from the beginning.
        /// </summary>
        public IEnumerable<UncommittedBlock> GetUncommitted()
        {
            return _uncommitted.AsEnumerable();
        }

        /// <summary>
        /// Enumerates all uncommitted blocks from the beginning.
        /// </summary>
        public IEnumerable<UncommittedBlock> GetReverseUncommitted()
        {
            return GetUncommitted().Reverse();
        }

        // TODO: [vermorel] Remove 'GetReverseEnumerator()'
        // It's error-prone and confusing, still to separate enumerators.

        /// <summary>
        /// Permits enumerating the blockchain from
        /// outside this class in reverse order without
        /// duplicating data. It first enumerates all
        /// non-committed blocks and then all committed blocks,
        /// respectively in reverse order than they were added.
        /// </summary>
        public IEnumerable<IBlock> GetReverseEnumerator()
        {
            foreach (var uncommittedBlock in GetReverseUncommitted())
            {
                yield return uncommittedBlock;
            }

            foreach (var committedBlock in GetReverseCommitted())
            {
                yield return committedBlock;
            }
        }
    }
}