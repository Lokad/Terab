// Copyright Lokad 2018 under MIT BCH.
using System;
using System.Buffers.Binary;
using Terab.Lib.Messaging;
using Xunit;

namespace Terab.Lib.Tests.Messaging
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
                _bytesRandom1 = new byte[Constants.MaxResponseSize - 4];
                _bytesRandom2 = new byte[Constants.MaxResponseSize - 20];

                rnd.NextBytes(_bytesRandom1);
                rnd.NextBytes(_bytesRandom2);

                BinaryPrimitives.TryWriteInt32LittleEndian(_bytesRandom1, Constants.MaxResponseSize - 4);
                BinaryPrimitives.TryWriteInt32LittleEndian(_bytesRandom2, Constants.MaxResponseSize - 20);
            }
        }
        // The size of the inbox should be bigger than the maximally
        // allowed message size
        private readonly BoundedInbox _inbox = new BoundedInbox(Constants.MaxResponseSize * 2);

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

        private void ReadingFromEmptyInbox()
        {
            Assert.Equal(0, _inbox.Peek().Length);
        }

        private void WritingToEmptyInbox()
        {
            Assert.True(_inbox.TryWrite(_bytes));
            Assert.Equal(_bytes, _inbox.Peek().ToArray());
        }

        private void ReadingMessageFromInbox()
        {
            // only works if inbox was filled with _bytes first and then _bytesRandom1
            PrepareBytesRandom();
            _inbox.TryWrite(_bytesRandom1);
            Assert.Equal(_bytes, _inbox.Peek().ToArray());
            _inbox.Next();
            Assert.Equal(_bytesRandom1, _inbox.Peek().ToArray());
        }

        private void TryReadingButNoMessageAvailable()
        {
            var counter = 0;
            for (;_inbox.CanPeek && counter < 100; ++counter)
            {
                _inbox.Peek();
                _inbox.Next();
            }

            Assert.Equal(1, counter);
        }

        private void TryWritingButInboxFull()
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

        private void ReadFromFullInbox()
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

        private void WriteIntoAndReadFromBuffer()
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
            var bytesLarge = new byte[Constants.MaxResponseSize + 5];
            rnd.NextBytes(bytesLarge);
            BinaryPrimitives.TryWriteInt32LittleEndian(bytesLarge, Constants.MaxResponseSize + 5);

            bytesLarge[0] = 0xE9;
            bytesLarge[1] = 0x03;
            bytesLarge[2] = 0x00;
            bytesLarge[3] = 0x00;
            Assert.Throws<ArgumentOutOfRangeException>(() => _inbox.TryWrite(bytesLarge));
        }

        [Fact]
        public void ConstructTooLittleInbox()
        {
            Assert.Throws<ArgumentException>(() => new BoundedInbox(Constants.MaxResponseSize - 1));
        }
    }
}
