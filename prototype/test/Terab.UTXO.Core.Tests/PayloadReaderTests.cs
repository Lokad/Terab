using System;
using Terab.UTXO.Core.Blockchain;
using Xunit;

namespace Terab.UTXO.Core.Tests
{
    public class PayloadReaderTests
    {
        [Fact]
        public void ReadSatoshis()
        {
            var spanToRead = new byte[50];
            BitConverter.TryWriteBytes(spanToRead, 20000UL);

            Assert.Equal(20000UL, PayloadReader.GetSatoshis(spanToRead));
        }

        [Fact]
        public void ReadSatoshisSpanTooShort()
        {
            var spanToRead = new byte[7];
            BitConverter.TryWriteBytes(spanToRead, 20000);
            Assert.Throws<ArgumentException>(() => PayloadReader.GetSatoshis(spanToRead));
        }

        [Fact]
        public void ReadData()
        {
            var spanToRead = new byte[50];
            BitConverter.TryWriteBytes(new Span<byte>(spanToRead).Slice(sizeof(ulong)), 20);
            spanToRead[sizeof(ulong) + sizeof(int)] = 25;
            spanToRead[sizeof(ulong) + sizeof(int) + 19] = 26;

            var data = PayloadReader.GetData(spanToRead);

            Assert.Equal(20, data.Length);
            Assert.Equal(25, data[0]);
            Assert.Equal(26, data[19]);
        }

        [Fact]
        public void ReadDataSpanTooShort()
        {
            var spanToRead = new byte[11];
            Assert.Throws<ArgumentException>(() => PayloadReader.GetData(spanToRead));
        }

        [Fact]
        public void EnumerateEvents()
        {
            var array = new byte[22];
            var spanToRead = array.AsSpan();
            var payloadLength = 2;
            BitConverter.TryWriteBytes(spanToRead.Slice(sizeof(ulong)), payloadLength);

            BitConverter.TryWriteBytes(spanToRead.Slice(sizeof(ulong) + sizeof(int) + payloadLength), 100);
            var consumption = 1 << 31 | 200;
            BitConverter.TryWriteBytes(spanToRead.Slice(sizeof(ulong) + sizeof(int) + payloadLength 
                                                         + BlockEvent.SizeInBytes), consumption);


            var iterator = PayloadReader.GetEvents(spanToRead);
            Assert.Equal(new BlockAlias(100), iterator.Current.BlockAlias);
            Assert.Equal(BlockEventType.Production, iterator.Current.Type);
            iterator.MoveNext();
            Assert.Equal(new BlockAlias(200), iterator.Current.BlockAlias);
            Assert.Equal(BlockEventType.Consumption, iterator.Current.Type);
            iterator.MoveNext();
            Assert.False(iterator.EndNotReached);
        }

        [Fact]
        public void ReadEventsSpanTooShort()
        {
            var spanToRead = new byte[13];
            BitConverter.TryWriteBytes(new Span<byte>(spanToRead).Slice(sizeof(ulong)), 2);
            Assert.Throws<ArgumentException>(() => PayloadReader.GetEvents(spanToRead).Current);
        }
    }
}