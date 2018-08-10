using System;
using System.Collections.Generic;
using System.Diagnostics;
using Terab.UTXO.Core.Blockchain;
using Terab.UTXO.Core.Hash;
using Terab.UTXO.Core.SectorStore;
using Terab.UTXO.Core.Serializer;

namespace Terab.UTXO.Core.Sozu
{
    /// <summary>
    /// Sozu table backed by persistent data storage.
    /// TXO are partitioned against their outpoints, and evicted
    /// based on their most recent block ('Spent' if available,
    /// 'Added' otherwise).
    /// </summary>
    /// <remarks>
    /// Sozu, the Japanese water fountain:
    /// https://en.wikipedia.org/wiki/Shishi-odoshi
    /// The TXOs accumulate into the Sozu store until it
    /// reaches its capacity, at which point, it flushes its
    /// content into the next layer of Sozu store.
    /// The Sozu store is NOT thread-safe. Thread-safety
    /// is ensured by making sure that the object is dedicated
    /// to a single thread which "owns" all interactions with
    /// the store.
    /// </remarks>
    public class SozuTable : ITxoTable
    {
        private readonly SozuConfig _config;

        private readonly ILineage _lineage;

        /// <summary>
        /// The sub-table is lazily initialized,
        /// remains null until a overflow triggers its instantiation.
        /// </summary>
        private SozuTable _subTable;

        private readonly ISectorStore _store;


        /// <summary>
        /// TxoPack used for collect txos not found by filter method.
        /// </summary>
        private TxoPack _residue;

        /// <summary>
        /// TxoPack used to store temporary outpoints during partition.
        /// </summary>
        private TxoPack _rangeOutputs;

        /// <summary>
        /// TxoPack used to receive the merge while writing.
        /// </summary>
        private TxoPack _merged;


        /// <summary>
        /// TxoPack used for sector deserialization.
        /// </summary>
        private TxoPack _deserialized;

        /// <summary>
        /// TxoPack used for collecting complements in filter operation.
        /// </summary>
        private TxoPack _complement;

        /// <summary>
        /// TxoPack used when writing to receive the pack to write here.
        /// </summary>
        private TxoPack _kept;

        /// <summary>
        /// TxoPack used when writing to receive the pack to be written downward.
        /// in the sub-tables.
        /// </summary>
        private TxoPack _flowDown;

        /// <param name="config"></param>
        /// <param name="lineage"></param>
        /// <param name="store"></param>
        /// <param name="subTable"></param>
        public SozuTable(SozuConfig config, ILineage lineage, ISectorStore store, SozuTable subTable = null)
        {
            _config = config;
            _lineage = lineage;
            _store = store;
            _subTable = subTable;

            _residue = TxoPack.Create();
            _rangeOutputs = TxoPack.Create();
            _deserialized = TxoPack.Create();
            _complement = TxoPack.Create();
            _kept = TxoPack.Create();
            _flowDown = TxoPack.Create();
            _merged = TxoPack.Create(2);
            store.Initialize();
            if (config.ImplicitSectors && !store.HasSectors())
            {
                store.AllocateSectors(1 << config.SectorBitDepth);
                EnsurePersistence();
            }
        }

        public Hash256 Start => _config.Start;

        public Hash256 End => _config.End;

        public void Read(TxoPack txoIn, TxoPack txoOut)
        {
            if (!_config.ImplicitSectors)
            {
                throw new InvalidOperationException();
            }

            txoOut.Clear();
            var partition = PartitionByImplicitSector(txoIn.GetForwardIterator());

            Read(partition, txoOut);
        }

        private void Read(IEnumerable<SectorRange> partition, TxoPack outputs)
        {
            foreach (var range in partition)
            {
                var sector = _store.Read(range.Index);
                sector.Deserialize(_deserialized, out var childSectorIndex);

                // We need not to overwrite txos, but put inside the payload found
                _deserialized.Filter(range.Outpoints, _kept, _residue, _complement);

                if (_residue.Count == 0 || childSectorIndex.IsUnknown() || _subTable == null)
                {
                    _flowDown.Clear();
                }
                else
                {
                // Recursive exploration
                    _subTable.Read(_residue, childSectorIndex, range.LeftTxid, _flowDown);
                }
                _flowDown.AddOrMerge(_kept.GetForwardIterator(), outputs, null);
            } // foreach (var range in partition)
        }

        /// <summary>
        /// Reading layers with explicit outpoint ranges.
        /// </summary>
        /// <param name="txoIn">txopack providing outpoints requesting a read</param>
        /// <param name="sectorIndex">Identifies the first sector to be read.</param>
        /// <param name="sectorHash">Identifies the hash range of the sector.</param>
        /// <param name="txoOut">output txoPack to collect read results</param>
        private void Read(TxoPack txoIn, SectorIndex sectorIndex, Hash256 sectorHash, TxoPack txoOut)
        {
            if (_config.ImplicitSectors)
            {
                throw new InvalidOperationException();
            }

            var partition = PartitionByExplicitSector(sectorIndex, sectorHash, txoIn.GetForwardIterator());

            Read(partition, txoOut);
        }

        public void Write(TxoPack txos)
        {
            if (!_config.ImplicitSectors)
            {
                throw new InvalidOperationException();
            }

            var partition = PartitionByImplicitSector(txos.GetForwardIterator());

            Write(partition, txos);
        }

        public void Write(TxoPack txos, SectorIndex sectorIndex, Hash256 sectorHash)
        {
            if (_config.ImplicitSectors)
            {
                throw new InvalidOperationException();
            }

            var partition = PartitionByExplicitSector(sectorIndex, sectorHash, txos.GetForwardIterator());

            Write(partition, txos);
        }
        public void EnsurePersistence()
        {
            _store.EnsurePersistence();
        }

        private void Write(IEnumerable<SectorRange> partition, TxoPack txos)
        {
            // 'partition' is expected to be aligned with local sectors
            foreach (var range in partition)
            {
                var childSectorIndex = SectorIndex.Unknown;
                if (_store.TryRead(range.Index, out var sector))
                {
                    sector.Deserialize(_deserialized, out childSectorIndex);
                }
                else
                {
                    _deserialized.Clear(); // make empty TxoPack
                }

                _deserialized.AddOrMerge(txos.GetForwardIterator(), _merged, _lineage);

                // extra TXOs fit locally
                if (_merged.IsOverflowing(sector.Length))
                {
                    if (_subTable == null)
                    {
                        // Normally everything should have been set up during initialization
                        throw new SozuTableOverflowException();
                    }

                    // TODO: [tandabany] WIP for the overflowing part
                    _merged.Split((int) (sector.Length * .5), _kept, _flowDown);

                    if (childSectorIndex.IsUnknown())
                    {
                        var deltaBitDepth = _subTable._config.SectorBitDepth - _config.SectorBitDepth;
                        childSectorIndex = _subTable._store.AllocateSectors(1 << deltaBitDepth);
                    }

                    sector.Serialize(_kept, childSectorIndex);
                    _store.Write(range.Index, sector);

                    _subTable.Write(_flowDown, childSectorIndex, range.LeftTxid);
                }
                else
                {
                    sector.Serialize(_merged, childSectorIndex);
                    _store.Write(range.Index, sector);
                }
            }
        }

        /// <param name="iter">
        /// The underlying TxoPack should be sorted in increasing order.
        /// </param>
        private IEnumerable<SectorRange> PartitionByImplicitSector(TxoForwardIterator iter)
        {
            return PartitionByExplicitSector(SectorIndex.First, Start, iter);
        }

        /// <param name="start">Context provided by the upper layer.</param>
        /// <param name="leftTxid">Context provided by the upper layer.</param>
        /// <param name="iter">Should be sorted in increasing order.</param>
        private IEnumerable<SectorRange> PartitionByExplicitSector(
            SectorIndex start,
            Hash256 leftTxid,
            TxoForwardIterator iter)
        {
            var localSectorTxoPackWriteAnchor = _rangeOutputs.GetResetWriter();

            var localSectorHash = leftTxid;

            // TODO: [vermorel] To be replaced by a regular 'foreach'
            for (; iter.EndNotReached; iter.MoveNext())
            {
                var outpoint = iter.Current;
                var canonicalHash = outpoint.Txid.Truncate(_config.SectorBitDepth);

                Debug.Assert(localSectorHash.CompareTo(canonicalHash) <= 0, "Unordered outpoints.");

                if (!canonicalHash.Equals(localSectorHash))
                {
                    // reach here means current outpoint from iterator is in different sector than
                    // localSectorHash, thus we need to flush what were written so far in writer
                    if (!localSectorTxoPackWriteAnchor.IsEmpty)
                    {
                        // this works because subSectors are all allocated once
                        var rangeIndex = start.GetNext(localSectorHash.Top(_config.SectorBitDepth) -
                                                       leftTxid.Top(_config.SectorBitDepth));
                        yield return new SectorRange(rangeIndex, localSectorHash, _rangeOutputs);

                        // recycling the list
                        localSectorTxoPackWriteAnchor = _rangeOutputs.GetResetWriter();
                    }

                    // flush done, we switch to next leftTxid
                    localSectorHash = canonicalHash;
                }

                localSectorTxoPackWriteAnchor.Write(in outpoint, ReadOnlySpan<byte>.Empty);
            }

            if (!localSectorTxoPackWriteAnchor.IsEmpty)
            {
                var rangeIndex = start.GetNext(localSectorHash.Top(_config.SectorBitDepth) -
                                               leftTxid.Top(_config.SectorBitDepth));
                yield return new SectorRange(rangeIndex, localSectorHash, _rangeOutputs);
            }
        }

        /// <summary>
        /// Intended to support partitioning outpoints into
        /// ranges that are aligned with their underlying sectors.
        /// </summary>
        private class SectorRange
        {
            /// <summary>
            /// Physical index referring to sectorStore.
            /// </summary>
            public SectorIndex Index { get; }

            /// <summary>
            /// Reference hash of this sectorRange.
            /// </summary>
            public Hash256 LeftTxid { get; }

            /// <summary>
            /// Outpoints within this sectorRange.
            /// </summary>
            public TxoPack Outpoints { get; }

            public SectorRange(SectorIndex index, Hash256 leftTxid, TxoPack outpoints)
            {
                Index = index;
                LeftTxid = leftTxid;
                Outpoints = outpoints;
            }
        }
    }
}