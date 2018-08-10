using System;
using Terab.UTXO.Core.Messaging;
using Xunit;

namespace Terab.UTXO.Core.Tests
{
    public class ClientServerMessageTests
    {
        private readonly byte[] _bytes =
        {
            0x11, 0x12, 0x13, 0x14, 0x15, 0x16, 0x17, 0x18,
            0x21, 0x22, 0x23, 0x24, 0x85, 0x26, 0x27, 0x28,
            0x31, 0x32, 0x33, 0x34, 0x35, 0x36, 0x37, 0x38
        };

        [Fact]
        public void TestLengthField()
        {
            ClientServerMessage.TryGetLength(_bytes, out var length);
            Assert.Equal(0x14131211, length);
            Assert.False(ClientServerMessage.TryGetLength(new Span<byte>(_bytes, 0, 3), out length));
        }

        [Fact]
        public void TestRequestIdField()
        {
            Assert.Equal(0x18171615U, ClientServerMessage.GetRequestId(_bytes));
            Assert.Throws<ArgumentException>(() => ClientServerMessage.GetRequestId(new Span<byte>(_bytes, 0, 7)));
        }

        [Fact]
        public void TestClientIdField()
        {
            Span<byte> expectedAsHex = stackalloc byte[] { 0x21, 0x22, 0x23, 0x24 };
            ClientId expected = ClientId.ReadFrom(expectedAsHex);
            Assert.Equal(expected, ClientServerMessage.GetClientId(_bytes));
            Assert.Throws<ArgumentException>(() => ClientServerMessage.GetClientId(new Span<byte>(_bytes, 0, 11)));
        }

        [Fact]
        public void TestSharded()
        {
            _bytes[12] = 0x85;
            Assert.True(ClientServerMessage.IsSharded(_bytes));

            _bytes[12] = 0x71;
            Assert.False(ClientServerMessage.IsSharded(_bytes));

            Assert.Throws<ArgumentException>(() => ClientServerMessage.IsSharded(new Span<byte>(_bytes, 0, 11)));
        }

        [Fact]
        public void TestRequestTypes()
        {
            BitConverter.TryWriteBytes(new Span<byte>(_bytes, ClientServerMessage.MessageTypeStart, sizeof(int)), (int)MessageType.OpenBlock);
            Assert.Equal(MessageType.OpenBlock, ClientServerMessage.GetMessageType(_bytes));

            BitConverter.TryWriteBytes(new Span<byte>(_bytes, ClientServerMessage.MessageTypeStart, sizeof(int)), (int)MessageType.CommitBlock);
            Assert.Equal(MessageType.CommitBlock, ClientServerMessage.GetMessageType(_bytes));

            BitConverter.TryWriteBytes(new Span<byte>(_bytes, ClientServerMessage.MessageTypeStart, sizeof(int)), (int)MessageType.Authenticate);
            Assert.Equal(MessageType.Authenticate, ClientServerMessage.GetMessageType(_bytes));

            BitConverter.TryWriteBytes(new Span<byte>(_bytes, ClientServerMessage.MessageTypeStart, sizeof(int)), (int)MessageType.GetBlockHandle);
            Assert.Equal(MessageType.GetBlockHandle, ClientServerMessage.GetMessageType(_bytes));

            BitConverter.TryWriteBytes(new Span<byte>(_bytes, ClientServerMessage.MessageTypeStart, sizeof(int)), (int)MessageType.IsAncestor);
            Assert.Equal(MessageType.IsAncestor, ClientServerMessage.GetMessageType(_bytes));

            BitConverter.TryWriteBytes(new Span<byte>(_bytes, ClientServerMessage.MessageTypeStart, sizeof(int)), (int)MessageType.IsPruneable);
            Assert.Equal(MessageType.IsPruneable, ClientServerMessage.GetMessageType(_bytes));

            Assert.Throws<ArgumentException>(() => ClientServerMessage.GetMessageType(new Span<byte>(_bytes, 0, ClientServerMessage.PayloadStart-1)));
        }

        [Fact]
        public void TestFirstKeyByte()
        {
            Assert.Equal(_bytes[ClientServerMessage.PayloadStart], ClientServerMessage.FirstKeyByte(_bytes));
            Assert.Throws<ArgumentException>(() => ClientServerMessage.FirstKeyByte(new Span<byte>(_bytes, 0, ClientServerMessage.PayloadStart)));
        }
    }
}
