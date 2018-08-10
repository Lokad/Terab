using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Terab.UTXO.Core.Blockchain;
using Terab.UTXO.Core.Serializer;
using Terab.UTXO.Core.Sozu;
using Xunit;

namespace Terab.UTXO.Core.Tests.TxoTest
{
    public class TxoPackFixture
    {
        public Random Random { get; }

        public BlockEvent[] Events { get; }

        public TxoPackFixture()
        {
            Random = new Random(60);
            var oldFormats = new int[12]
            {
                -654321, -123456, -100, -5, 518, 730, 9753, 31086, 40000, 200_000, 210_000,
                210_001
            };
            Events = new BlockEvent[12];
            for (int i = 0; i < 12; i++)
            {
                var alias = new BlockAlias((uint) Math.Abs(oldFormats[i]));
                var type = oldFormats[i] > 0 ? BlockEventType.Consumption : BlockEventType.Production;
                Events[i] = new BlockEvent(alias, type);
            }

            // old format ordering (signed int) is different than BlockEvent ordering,
            // we sort using BlockEvent ordering
            Array.Sort(Events);
        }
    }

    public class TxoPackTest : IClassFixture<TxoPackFixture>
    {
        private readonly Random _rand;

        private readonly BlockEvent[] _events;

        private const int PayloadLengthLowLimit = 60;

        private const int PayloadLengthUpLimit = 256;

        public TxoPackTest(TxoPackFixture fixture)
        {
            _rand = fixture.Random;
            _events = fixture.Events;
        }

        /// <summary>
        /// Return a random outpoint and a random payload.
        /// </summary>
        private (Outpoint, Memory<byte>) MakeRandomRawTxo(int lowLimit, int upLimit)
        {
            var content = new byte[Outpoint.TotalSizeInBytes].AsMemory();
            var temp = new byte[Outpoint.KeySizeInBytes - 4];
            _rand.NextBytes(temp);
            temp.CopyTo(content);

            var index = _rand.Next(0, 20);
            BitConverter.GetBytes(index).CopyTo(content.Slice(Outpoint.KeySizeInBytes - 4, 4));

            var payloadLength = _rand.Next(lowLimit, upLimit);
            BitConverter.GetBytes(payloadLength).CopyTo(content.Slice(Outpoint.KeySizeInBytes, 4));

            var payload = new byte[payloadLength];
            _rand.NextBytes(payload);

            var numberOfEvents = _rand.Next(1, 5);
            // write data length inside payload

            BitConverter.TryWriteBytes(payload.AsSpan(sizeof(ulong), sizeof(int)),
                payloadLength - sizeof(ulong) - sizeof(int) - BlockEvent.SizeInBytes * numberOfEvents);

            var writer = new PayloadWriter(payload.AsSpan(payloadLength - BlockEvent.SizeInBytes * numberOfEvents));
            for (var i = 0; i < numberOfEvents; ++i)
            {
                var ev = new BlockEvent((uint) _rand.Next(0, 1_000_000));
                writer.Write(ev);
            }

            return (Outpoint.Create(content.Span), payload);
        }

        /// <summary>
        /// return a random TxoPack for a given number of outpoint inside
        /// </summary>
        /// <param name="number">number of outpoints</param>
        private TxoPack MakeRandomTxoPack(int number)
        {
            var outpointsArray = new Outpoint[number];
            var payloadStream = new MemoryStream();

            var listTxo = new List<ValueTuple<Outpoint, ReadOnlyMemory<byte>>>(number);

            for (var i = 0; i < number; i++)
            {
                listTxo.Add(MakeRandomRawTxo(PayloadLengthLowLimit, PayloadLengthUpLimit));
            }

            listTxo.Sort((a, b) => a.Item1.CompareTo(b.Item1));
            var j = 0;
            foreach (var (outpoint, payload) in listTxo)
            {
                outpointsArray[j] = outpoint;
                j++;
                payloadStream.Write(payload.ToArray(), 0, payload.Length);
            }

            return new TxoPack(outpointsArray, payloadStream.ToArray());
        }

        /// <summary>
        /// Test AddOrMerge, no outpoints are equal in this case
        /// </summary>
        [Fact]
        public void AddWithoutMergeTest()
        {
            const int testSize = 5;
            var txoPack = MakeRandomTxoPack(testSize);
            var anotherTxoPack = MakeRandomTxoPack(testSize);

            var resultTxoPack = TxoPack.Create(2);

            txoPack.AddOrMerge(anotherTxoPack.GetForwardIterator(), resultTxoPack, null);

            // ensure number of outpoints
            Assert.True(testSize * 2 == resultTxoPack.Count, "Merged outpoints total number mismatch.");


            // ensure that merged TxoPack has sorted keys;
            var iter = resultTxoPack.GetForwardIterator();
            ref var op = ref iter.Current;
            iter.MoveNext();
            var wellOrdered = true;
            var sumOfLength = op.PayloadLength;
            while (iter.EndNotReached)
            { 
                wellOrdered = wellOrdered && op.CompareTo(iter.Current) == -1;

                op = iter.Current;

                sumOfLength += iter.Current.PayloadLength;
                iter.MoveNext();
            }

            Assert.True(wellOrdered, "Result of AddOrMerge not well ordered");

            // ensure size of payloads
            Assert.True(txoPack.Payloads.Length + anotherTxoPack.Payloads.Length ==
                        sumOfLength, "Merged payloads total length mismatch.");
            Assert.True(txoPack.Count + anotherTxoPack.Count == resultTxoPack.Count);
        }

        [Fact]
        public void MergeOutpoints()
        {
            var (outpoint, payload) = MakeRandomRawTxo(PayloadLengthLowLimit, PayloadLengthUpLimit);
            var eventsMemory = CreateWithTestEvents(new[] {0, 3, 4, 6, 8, 9, 10, 11});

            var dataLength = payload.Length - eventsMemory.Length - sizeof(ulong) - sizeof(int);
            BitConverter.TryWriteBytes(payload.Slice(sizeof(ulong)).Span, dataLength);
            eventsMemory.CopyTo(payload.Slice(payload.Length - eventsMemory.Length));

            var eventsMemory2 = CreateWithTestEvents(new[] {1, 2, 5, 6, 7});

            var toMergeLength = payload.Length - eventsMemory.Length + eventsMemory2.Length;
            var payloadToMerge = new byte[toMergeLength];
            payload.Slice(0, toMergeLength).CopyTo(payloadToMerge);
            eventsMemory2.CopyTo(payloadToMerge.AsMemory().Slice(toMergeLength - eventsMemory2.Length));

            var outpointExt = new byte[Outpoint.TotalSizeInBytes].AsMemory();
            outpoint.WriteTo(outpointExt.Span);
            BitConverter.TryWriteBytes(outpointExt.Span.Slice(Outpoint.KeySizeInBytes), toMergeLength);

            PrintBytes(payload.ToArray());
            PrintBytes(payloadToMerge);

            var targetPack = TxoPack.Create();
            var originPack = new TxoPack(new[] {outpoint}, payload);
            var toMergePack = new TxoPack(new[] {Outpoint.Create(outpointExt.Span)}, payloadToMerge);

            originPack.AddOrMerge(toMergePack.GetForwardIterator(), targetPack, null);

            var mergedPayload = targetPack.GetForwardIterator().CurrentPayload;
            Assert.Equal(PayloadReader.GetSatoshis(payload.Span), PayloadReader.GetSatoshis(mergedPayload));
            Assert.Equal(PayloadReader.GetDataLength(payload.Span), PayloadReader.GetDataLength(mergedPayload));
            Assert.True(PayloadReader.GetData(payload.Span).SequenceEqual(PayloadReader.GetData(mergedPayload)));

            var iteration = 0;
            for (var iter = PayloadReader.GetEvents(mergedPayload); iter.EndNotReached; iter.MoveNext(), iteration++)
            {
                Assert.True(_events[iteration].Equals(iter.Current), "Expected merged events mismatch.");
            }

            Assert.True(5 == PayloadReader.GetAge(mergedPayload), "Expected payload age mismatches.");
        }

        public Memory<byte> CreateWithTestEvents(int[] eventSubArray)
        {
            byte[] resArray = new byte[_events.Length * BlockEvent.SizeInBytes];

            var payloadWriter = new PayloadWriter(resArray);

            foreach (var i in eventSubArray)
            {
                payloadWriter.Write(_events[i]);
            }

            return resArray.AsMemory().Slice(0, payloadWriter.Offset);
        }


        public static void PrintBytes(byte[] byteArray)
        {
            var sb = new StringBuilder("{ ");
            for (var i = 0; i < byteArray.Length; i++)
            {
                var b = byteArray[i];
                sb.Append(b);
                if (i < byteArray.Length - 1)
                {
                    sb.Append(", ");
                }
            }

            sb.Append(" }");
            Console.WriteLine(sb.ToString());
        }

        [Fact]
        public void FilterTest()
        {
            const int originalPackSize = 21;
            const int notFoundSize = 5;

            var txoPack = MakeRandomTxoPack(originalPackSize);

            var pickedOutpoints = new List<Outpoint>();
            for (var iter = txoPack.GetForwardIterator(); iter.EndNotReached; iter.MoveNext())
            {
                if (_rand.Next() % 3 == 0)
                {
                    pickedOutpoints.Add(iter.Current);
                }
            }

            var joinFilterSize = pickedOutpoints.Count;

            // append some other outpoints
            var anotherPack = MakeRandomTxoPack(notFoundSize);
            for (var iter = anotherPack.GetForwardIterator(); iter.EndNotReached; iter.MoveNext())
            {
                pickedOutpoints.Add(iter.Current);
            }

            pickedOutpoints.Sort();

            var filterPack = new TxoPack(pickedOutpoints.ToArray(), null);
            var foundPack = TxoPack.Create();
            var complementPack = TxoPack.Create();
            var notFoundOutpoints = TxoPack.Create();


            txoPack.Filter(filterPack, foundPack, notFoundOutpoints, complementPack);

            // verify filter resulting packs counts
            Assert.True(joinFilterSize == foundPack.Count, "foundPack outpoints number mismatch.");
            Assert.True(originalPackSize - joinFilterSize == complementPack.Count,
                "complement outpoints number mismatch.");
            Assert.True(notFoundSize == notFoundOutpoints.Count, "not found outpoints number mismatch.");

            // verify by AddOrMerge that we have exactly originalPack
            foundPack.AddOrMerge(complementPack.GetForwardIterator(), notFoundOutpoints, null);

            Assert.True(txoPack.Count == notFoundOutpoints.Count);
            for (TxoForwardIterator iter1 = txoPack.GetForwardIterator(),
                iter2 = notFoundOutpoints.GetForwardIterator();
                iter1.EndNotReached && iter2.EndNotReached;
                iter1.MoveNext(), iter2.MoveNext())
            {
                Assert.True(iter1.Current == iter2.Current, "Rebuild pack outpoint mismatch.");
                Assert.True(iter1.CurrentPayload.Length == iter2.CurrentPayload.Length,
                    "Rebuild pack payload length mismatch.");
                Assert.True(iter1.CurrentPayload.SequenceEqual(iter2.CurrentPayload),
                    "Deserialized pack payload content mismatch");
            }
        }

        /// <summary>
        /// Test bound condition where there is not complement outpoints neither
        /// not found outpoints after filtering
        /// </summary>
        [Fact]
        public void FilterBoundConditionsTest()
        {
            var txoPack = MakeRandomTxoPack(15);
            var foundPack = TxoPack.Create();
            var complementPack = TxoPack.Create();
            var notFoundOutpoints = TxoPack.Create();

            txoPack.Filter(txoPack, foundPack, notFoundOutpoints, complementPack);
            Assert.Equal(15, foundPack.Count);
            Assert.Equal(0, complementPack.Count);
            Assert.Equal(0, notFoundOutpoints.Count);
        }

        /// <summary>
        /// Test TxoPack split.
        /// </summary>
        [Fact]
        public void SplitTest()
        {
            var maxSize = 1024 * 4;
            var count1 = 10;
            var count2 = 11;
            var txoPack = MakeRandomTxoPack(count1);
            var txoPack2 = MakeRandomTxoPack(count2);
            var mergedPack = TxoPack.Create(2);

            var keepPack = TxoPack.Create();
            var flowPack = TxoPack.Create();

            txoPack.AddOrMerge(txoPack2.GetForwardIterator(), mergedPack, null);
            // unit test uses payload span length instead of read age
            mergedPack.Split(maxSize, keepPack, flowPack);
            //mergedPack.Split(maxSize, keepPack, flowPack,  (x=>PayloadReader.GetAge(x.CurrentPayload)));
            Assert.False(keepPack.IsOverflowing(maxSize));
            Assert.True(keepPack.VerifyOutpointsOrdering());
            Assert.True(flowPack.VerifyOutpointsOrdering());
        }

        /// <summary>
        /// Test TxoPack serialization.
        /// </summary>
        [Fact]
        public void TestSerialization()
        {
            const int count = 9;
            var txoPack = MakeRandomTxoPack(count);
            var txoPackLarge = TxoPack.Create();

            // sector size matches our txoPackLarge size
            var sector = new Sector(Outpoint.TotalSizeInBytes * Constants.MaxOutpointsCount +
                                    count * Consensus.MaxScriptSizeInBytes);
            sector.Serialize(txoPack, SectorIndex.Unknown);
            sector.Deserialize(txoPackLarge, out var index);

            Assert.True(index.IsUnknown());
            Assert.True(txoPack.Count == txoPackLarge.Count);
            for (TxoForwardIterator iter1 = txoPack.GetForwardIterator(), iter2 = txoPackLarge.GetForwardIterator();
                iter1.EndNotReached && iter2.EndNotReached;
                iter1.MoveNext(), iter2.MoveNext())
            {
                Assert.True(iter1.Current == iter2.Current, "Deserialized pack outpoint mismatch.");
                Assert.True(iter1.CurrentPayload.Length == iter2.CurrentPayload.Length,
                    "Deserialized pack payload length mismatch.");
                Assert.True(iter1.CurrentPayload.SequenceEqual(iter2.CurrentPayload),
                    "Deserialized pack payload content mismatch");
            }
        }
    }
}