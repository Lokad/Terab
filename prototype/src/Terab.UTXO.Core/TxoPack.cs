using System;
using System.Diagnostics;
using Terab.UTXO.Core.Blockchain;

namespace Terab.UTXO.Core
{
    /// <summary>
    /// A set of TXO entries, ordered by outpoint, lowest first, according to
    /// canonical ordering.
    /// Main operating unit for SozuTable.
    /// </summary>
    public class TxoPack
    {
        /// <summary>
        /// Factory method for create standard size TxoPack or TxoPack having
        /// whole multiple of standard size
        /// </summary>
        /// <param name="multiple">
        /// 1 for standard, 2 for magnum, 4 for Jeroboam etc
        /// </param>
        public static TxoPack Create(int multiple = 1)
        {
            if (multiple > 4 || multiple < 1)
                throw new ArgumentOutOfRangeException(nameof(multiple));

            return new TxoPack(new Outpoint[Constants.MaxOutpointsCount * multiple],
                new byte[Constants.MaxPayloadsCapacity * multiple]);
        }

        //Fields array allocated and dedicated for Split method.
        //Initialized in constructor
        private readonly uint[] __ages;
        private readonly ushort[] __argsort;
        private readonly int[] __payloadsLength;
        private readonly bool[] __elementOrders;

        /// <summary>
        /// Intended as the backing for outpoints.
        /// </summary>
        /// <remarks>
        /// Storage capacity is allocated generously, which means that
        /// potentially more space is available than will be filled by
        /// outpoints. As the storage will be reused, the additional space might
        /// not be empty, but can contain data from the previous usage.
        /// Use ForwardIterator <see cref="TxoForwardIterator.Current"/> to
        /// iterate through the currently valid outpoints.
        /// </remarks>
        private Memory<Outpoint> _outpoints;

        /// <summary>
        /// Intended as the backing for payloads.
        /// Outpoint offset indicates corresponding payload length.
        /// </summary>
        /// <remarks>
        /// Storage capacity is allocated generously, which means that
        /// potentially more space is available than will be filled by payloads.
        /// As the storage will be reused, the additional space might
        /// not be empty, but can contain data from the previous usage.
        /// Use ForwardIterator <see cref="TxoForwardIterator.CurrentPayload"/>
        /// to iterate through the currently valid payloads.
        /// </remarks>
        private Memory<byte> _payloads;

        /// <summary>
        /// unique writer available for this instance of txoPack
        /// </summary>
        private TxoPackWriter _writer;


        /// <summary>
        /// Number of useful outpoints
        /// </summary>
        private int _count;

        /// <summary>
        /// Length of useful payloads
        /// It let us to know where to cut in generously allocated
        /// payloads byte memory
        /// </summary>
        private int _payloadsLength;

        /// <summary>
        /// TxoPack constructor which gives memory allocations tailored to the
        /// amount of outpoints/payloads which are stored inside. No unused
        /// storage should trail at the end of the valid outpoints/payloads.
        /// </summary>
        /// <remarks>
        /// As TxoPack is pooled, the constructor is rarely used.
        /// </remarks>
        /// <param name="outpoints">
        /// Canonically ordered outpoints in tightly fitting memory.
        /// </param>
        /// <param name="payloads">
        /// Corresponding payloads in tightly fitting memory.
        /// </param>
        public TxoPack(Memory<Outpoint> outpoints, Memory<byte> payloads)
        {
            _outpoints = outpoints;
            _payloads = payloads;
            _count = outpoints.Length;
            _payloadsLength = payloads.Length;

            // Initialize Split dedicated fields
            __ages = new uint[Count];
            __argsort = new ushort[Count];
            __payloadsLength = new int[Count];
            __elementOrders = new bool[Count];

            _writer = new TxoPackWriter(this);
        }

        public Span<Outpoint> OutPoints => _outpoints.Span;

        public Span<byte> Payloads => _payloads.Span;

        public int Count
        {
            get => _count;
            set => _count = value;
        }

        public int PayloadsLength
        {
            get => _payloadsLength;
            set => _payloadsLength = value;
        }

        /// <summary>
        /// Provide forward iterator on current TxoPack.
        /// </summary>
        public TxoForwardIterator GetForwardIterator()
        {
            return new TxoForwardIterator(this);
        }

        /// <summary>
        /// Provide the writer on the current TxoPack.
        /// One can append content to this writer.
        /// </summary>
        public ref TxoPackWriter GetOngoingWriter()
        {
            return ref _writer;
        }


        /// <summary>
        /// Reset the writer on the current TxoPack and return it.
        /// </summary>
        public ref TxoPackWriter GetResetWriter()
        {
            Clear();
            return ref _writer;
        }

        /// <summary>
        /// Shallow clear content by setting both outpoints count and
        /// payloadsLength to zero.
        /// </summary>
        public void Clear()
        {
            _count = 0;
            _payloadsLength = 0;
            _writer.Reset();
        }

        /// <summary>
        /// Add the payload or merge its content into the existing one if a
        /// payload already exists.
        /// The lineage is provided in order to ensure that the merge is
        /// consistent.
        /// </summary>
        /// <param name="other">external TxoPack to add or merge with</param>
        /// <param name="target">TxoPack storing result of AddOrMerge</param>
        /// <param name="lineage">lineage helping the AddOrMerge</param>
        public void AddOrMerge(TxoForwardIterator other, TxoPack target, ILineage lineage)
        {
            var self = GetForwardIterator();
            ref var writeAnchor = ref target.GetResetWriter();

            while (self.EndNotReached && other.EndNotReached)
            {
                ref var o1 = ref self.Current;
                ref var o2 = ref other.Current;
                var resCompare = o1.CompareTo(o2);
                if (resCompare == -1)
                {
                    writeAnchor.Write(in o1, self.CurrentPayload);
                    self.MoveNext();
                }
                else if (resCompare == 1)
                {
                    writeAnchor.Write(in o2, other.CurrentPayload);
                    other.MoveNext();
                }
                else
                {
                    var payloadWriter = new PayloadWriter(writeAnchor.RemainingPayload);
                    PayloadReader.MergePayloads(self.CurrentPayload, other.CurrentPayload, ref payloadWriter);
                    // duplicated Outpoint struct and set the right payload length after merge.
                    var o = new Outpoint(o1.Txid, o1.Index, payloadWriter.Offset, o1.Flags | o2.Flags);
                    writeAnchor.Write(in o, ReadOnlySpan<byte>.Empty);
                    self.MoveNext();
                    other.MoveNext();
                }
            }

            writeAnchor.WriteMany(ref self);
            writeAnchor.WriteMany(ref other);
        }


        /// <summary>
        /// The goal of this method is to compare a given TxoPack with this one.
        /// The comparison yields the intersection of the two sets, the 'rest'
        /// of this set which has no correspondence in the given set, and the
        /// 'rest' of the given set which has no correspondence in this set.
        /// In the variables, the given TxoPack is referred to as the 'filter'.
        /// The method can be used when looking for given txos (filter) in the
        /// sozu table. The method compares the researched txos with this
        /// TxoPack, and returns as answer the ones that have been found and the
        /// ones that haven't. The ones that haven't been found can be
        /// researched in deeper layers of the sozu table.
        /// </summary>
        /// <param name="filter">
        /// This will typically contain txos that are to be found in the sozu
        /// table, and more precisely in this TxoPack.
        /// </param>
        /// <param name="found">
        /// Provided memory space that will be filled with the intersecting
        /// outpoints and payloads.
        /// </param>
        /// <param name="notFound">
        /// Provided memory space that will be filled with the outpoints in the
        /// filter that have not been found in this TxoPack.
        /// </param>
        /// <param name="complement">
        /// Provided memory space that will be filled with the elements of this
        /// TxoPack that were not present in the filter.
        /// </param>
        /// <remarks>
        /// The union of the three returned sets <see cref="found"/>,
        /// <see cref="notFound"/> and <see cref="complement"/>
        /// is the same as the union of this set and the set given via
        /// <see cref="filter"/>.
        /// </remarks>
        public void Filter(TxoPack filter, TxoPack found, TxoPack notFound, TxoPack complement)
        {
            var self = GetForwardIterator();
            var filterIterator = filter.GetForwardIterator();
            ref var writer = ref found.GetResetWriter();

            ref var notFoundWriter = ref notFound.GetResetWriter();
            ref var complementWriter = ref complement.GetResetWriter();

            while (self.EndNotReached && filterIterator.EndNotReached)
            {
                ref var o1 = ref self.Current;
                ref var o2 = ref filterIterator.Current;
                var resCompare = o1.CompareTo(o2);
                if (resCompare == -1)
                {
                    // current outpoint of this object will never match any outpoint in filter
                    complementWriter.Write(in o1, self.CurrentPayload);
                    self.MoveNext();
                }
                else if (resCompare == 1)
                {
                    // filter outpoint will never be found
                    notFoundWriter.Write(in o2, ReadOnlySpan<byte>.Empty);
                    filterIterator.MoveNext();
                }
                else
                {
                    writer.Write(in o1, self.CurrentPayload);
                    self.MoveNext();
                    filterIterator.MoveNext();
                }

                Debug.Assert(found.Count + complement.Count <= Count);
                Debug.Assert(found.Count + notFound.Count <= filter.Count);
            }

            complementWriter.WriteMany(ref self);
            notFoundWriter.WriteMany(ref filterIterator);
            Debug.Assert(found.Count + complement.Count == Count);
            Debug.Assert(found.Count + notFound.Count == filter.Count);
        }

        /// <summary>
        /// Determine whether this TxoPack exceeds the given sizeInBytes and is
        /// therefore considered overflowing.
        /// </summary>
        /// <param name="sizeInBytes">Size in bytes.</param>
        /// <returns>
        /// True if sizeInBytes of TxoPack is superior or equal to the given
        /// size in bytes, false otherwise.
        /// </returns>
        public bool IsOverflowing(int sizeInBytes)
        {
            return PayloadsLength + Count * Outpoint.TotalSizeInBytes >= sizeInBytes;
        }

        /// <summary>
        /// Split this TxoPack into two distinct ones, based on a given scoring
        /// function.
        /// More the outpoint has higher score, more likely it is to go down to
        /// the next layer of the SozuTable.
        /// If score is age, more the outpoint is "old", more likely it is to go
        /// down to the next layer of the SozuTable.
        /// <see cref="PayloadReader.GetAge"/>
        /// </summary>
        /// <remarks>
        /// The method can be used to determine which txos to descend in the
        /// SozuTable:
        /// Typically, the first pack would stay at current layer of the
        /// SozuTable, the second one would go down to the next layer.
        /// </remarks>
        /// <param name="maxSize">Size limit of kept pack.</param>
        /// <param name="kept">
        /// Provided memory space that will be filled with the
        /// TxoPack that will stay at the current layer.
        /// </param>
        /// <param name="flowdown">
        /// Provided memory space that will be filled with the TxoPack that will
        /// flow down.
        /// </param>
        public void Split(int maxSize, TxoPack kept, TxoPack flowdown)
        {
            // ages example:           [4, 6 ,2, 20 ,7, 1 ,5, 17, 7]
            // argsort example :       [5, 2, 0, 6, 1, 4, 8, 7, 3] 
            // elementOrders example : [True, True, True, False, False, True, True, False, False]

            ushort i = 0;
            for (var iter = GetForwardIterator(); iter.EndNotReached; iter.MoveNext(), i++)
            {
                __ages[i] = PayloadReader.GetAge(iter.CurrentPayload);
                __argsort[i] = i;
                __payloadsLength[i] = iter.Current.PayloadLength;
            }


            // TODO: when Span.Sort extension method would be released, one can use stackalloc array instead of memeber fields

            Array.Sort(__ages, __argsort, 0, Count);

            // aggregate payloadsLength on sorted order
            var sumSize = 0;
            var keepFlag = true;
            for (i = 0; i < Count; i++)
            {
                var length = __payloadsLength[__argsort[i]];
                sumSize += length + Outpoint.TotalSizeInBytes;
                // one continue to keep the current outpoint
                // as far as the size of keptPack is limited by maxSize
                keepFlag &= sumSize < maxSize;
                __elementOrders[__argsort[i]] = keepFlag;
            }

            ref var keptWriter = ref kept.GetResetWriter();
            ref var flowdownWriter = ref flowdown.GetResetWriter();

            i = 0;
            for (var iter = GetForwardIterator(); iter.EndNotReached; iter.MoveNext(), ++i)
            {
                if (__elementOrders[i])
                    keptWriter.Write(in iter.Current, iter.CurrentPayload);
                else
                    flowdownWriter.Write(in iter.Current, iter.CurrentPayload);
            }
        }

        /// <summary>
        /// Verify canonical ordering of this pack. A loop into outpoints is
        /// performed.
        /// </summary>
        /// <returns>
        /// True if outpoints are canonically ordered, false otherwise.
        /// </returns>
        public bool VerifyOutpointsOrdering()
        {
            // When there is a single outpoint in TxoPack, below iter.MoveNext() would fail,
            // what this condition check try to prevent happening.
            if (Count <= 1)
                return true;

            var iter = GetForwardIterator();
            ref var op = ref iter.Current;
            iter.MoveNext();
            for (; iter.EndNotReached; iter.MoveNext())
            {
                if (op.CompareTo(iter.Current) != -1)
                {
                    return false;
                }

                op = iter.Current;
            }

            return true;
        }
    }
}