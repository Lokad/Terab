using System;
using Terab.UTXO.Core.Messaging;
using Xunit;

namespace Terab.UTXO.Core.Tests
{
    public class BoundedInboxTests
    {
        private readonly byte[] _bytes =
        {
            0x10, 0x00, 0x00, 0x00, 0x15, 0x16, 0x17, 0x18,
            0x21, 0x22, 0x23, 0x24, 0x25, 0x26, 0x27, 0x28
        };

        private byte[] _bytesRandom1;
        private byte[] _bytesRandom2;

        private void PrepareBytesRandom()
        {
            if (_bytesRandom1 == null)
            {
                var rnd = new Random(42);
                // very large messages
                _bytesRandom1 = new byte[ClientServerMessage.MaxSizeInBytes - 4];
                _bytesRandom2 = new byte[ClientServerMessage.MaxSizeInBytes - 20];

                rnd.NextBytes(_bytesRandom1);
                rnd.NextBytes(_bytesRandom2);

                BitConverter.TryWriteBytes(_bytesRandom1, ClientServerMessage.MaxSizeInBytes - 4);
                BitConverter.TryWriteBytes(_bytesRandom2, ClientServerMessage.MaxSizeInBytes - 20);
            }
        }
        // The size of the inbox should be bigger than the maximally
        // allowed message size
        private readonly BoundedInbox _inbox = new BoundedInbox(ClientServerMessage.MaxSizeInBytes * 2);

        [Fact]
        public void ExecuteTestsInOrder()
        {
            ReadingFromEmptyInbox();

            WritingToEmptyInbox();

            ReadingMessageFromInbox();

            TryReadingButNoMessageAvailable();

            TryWritingButInboxFull();

            ReadFromFullInbox();

            WriteIntoAndReadFromBuffer();
        }

        public void ReadingFromEmptyInbox()
        {
            Assert.Equal(0, _inbox.Peek().Length);
        }

        public void WritingToEmptyInbox()
        {
            Assert.True(_inbox.TryWrite(_bytes));
            Assert.Equal(_bytes, _inbox.Peek().ToArray());
        }

        public void ReadingMessageFromInbox()
        {
            // only works if inbox was filled with _bytes first and then _bytesRandom1
            PrepareBytesRandom();
            _inbox.TryWrite(_bytesRandom1);
            Assert.Equal(_bytes, _inbox.Peek().ToArray());
            _inbox.Next();
            Assert.Equal(_bytesRandom1, _inbox.Peek().ToArray());
        }

        public void TryReadingButNoMessageAvailable()
        {
            int counter = 0;
            while (_inbox.Peek().Length > 0 && counter < 100)
            {
                _inbox.Next();
                counter++;
            }
            // depending on which tests have been executed before, counter might change
            // In this configuration, counter should be exactly at 1
            Assert.Equal(1, counter);
        }

        public void TryWritingButInboxFull()
        {
            // TryReadingButNoMessageAvailable(); - to be executed if tests are not done in order
            PrepareBytesRandom();
            Assert.True(_inbox.TryWrite(_bytes));
            Assert.True(_inbox.TryWrite(_bytesRandom1));
            Assert.False(_inbox.TryWrite(_bytesRandom1));
            Assert.True(_inbox.TryWrite(_bytesRandom2));
            Assert.False(_inbox.TryWrite(_bytes));
            Assert.False(_inbox.TryWrite(_bytesRandom2));
        }

        public void ReadFromFullInbox()
        {
            // TryWritingButInboxFull(); - to be executed if tests are not done in order
            Assert.Equal(_bytes, _inbox.Peek().ToArray());
            _inbox.Next();
            Assert.Equal(_bytesRandom1, _inbox.Peek().ToArray());
            _inbox.Next();
            Assert.Equal(_bytesRandom2, _inbox.Peek().ToArray());
            _inbox.Next();
            Assert.Equal(0, _inbox.Peek().Length);
        }

        public void WriteIntoAndReadFromBuffer()
        {
            // TryReadingButNoMessageAvailable(); - to be executed if tests are not done in order

            // 125 * 16 = 2000
            for (int i = 0; i < 126; i++)
            {
                _bytes[15] = (byte)i;
                Assert.True(_inbox.TryWrite(_bytes));
                Assert.Equal(_bytes, _inbox.Peek().ToArray());
                _inbox.Next();
            }
        }

        [Fact]
        public void AnnouncingWrongMessageLength()
        {
            _bytes[3] = 0x55;
            Assert.Throws<ArgumentOutOfRangeException>(() => _inbox.TryWrite(_bytes));
            _bytes[3] = 0x00;
        }

        [Fact]
        public void TooLargeMessageLength()
        {
            var rnd = new Random(42);
            var bytesLarge = new byte[ClientServerMessage.MaxSizeInBytes + 5];
            rnd.NextBytes(bytesLarge);
            BitConverter.TryWriteBytes(bytesLarge, ClientServerMessage.MaxSizeInBytes + 5);

            bytesLarge[0] = 0xE9;
            bytesLarge[1] = 0x03;
            bytesLarge[2] = 0x00;
            bytesLarge[3] = 0x00;
            Assert.Throws<ArgumentOutOfRangeException>(() => _inbox.TryWrite(bytesLarge));
        }

        [Fact]
        public void ConstructTooLittleInbox()
        {
            Assert.Throws<ArgumentException>(() => new BoundedInbox(ClientServerMessage.MaxSizeInBytes - 1));
        }
    }
}
