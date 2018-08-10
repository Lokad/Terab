using System;
using Terab.UTXO.Core.Messaging;
using Terab.UTXO.Core.Networking;
using Xunit;

namespace Terab.UTXO.Core.Tests
{
    public class ClientConnectionTests
    {
        private readonly MockSocket Socket;
        private readonly ClientConnection _cc;

        public ClientConnectionTests()
        {
            Socket = new MockSocket();
            _cc = new ClientConnection(Socket, ClientId.Next(), 4000);
        }

        private readonly byte[] _bytes1K = new byte[1000];
        private readonly byte[] _bytes996 = new byte[996];
        private readonly byte[] _bytes100 = new byte[100];
        private readonly byte[] _bytes10 = new byte[10];
        private readonly byte[] _bytes4 = new byte[4];

        private void PrepareArrays()
        {
            if (_bytes4[0] == 0x00)
            {
                //Console.WriteLine($"{1000:X16}");
                //Console.WriteLine($"{996:X16}");
                //Console.WriteLine($"{100:X16}");
                //Console.WriteLine($"{10:X16}");
                //Console.WriteLine($"{2:X16}");
                _bytes1K[0] = 0xE8;
                _bytes1K[1] = 0x03;

                _bytes996[0] = 0xE4;
                _bytes996[1] = 0x03;

                _bytes100[0] = 0x64;
                _bytes100[1] = 0x00;

                _bytes10[0] = 0x0A;
                _bytes10[1] = 0x00;

                _bytes4[0] = 0x04;
                _bytes4[1] = 0x00;
            }
            
        }

        [Fact]
        public void WriteCallsSendIfNotFull()
        {
            PrepareArrays();
            Socket.ExpectConnected(() => true);
           
            // Test 'write'
            Socket.ExpectSend(data => {
                Assert.Equal(100, data.Length);
                return 5;
            });
            
            _cc.TryWrite(_bytes100);

            Socket.ExpectAllDone();

            // Test 'Send' after incomplete write

            Socket.ExpectConnected(() => true);

            Socket.ExpectSend(data =>
            {
                Assert.Equal(95, data.Length);
                return 95;
            });

            _cc.Send();

            Socket.ExpectAllDone();

            // Test that all is done.

            Socket.ExpectConnected(() => true);
            
            _cc.Send();

            Socket.ExpectAllDone();            
        }

        [Fact]
        public void FillUpExitBuffer()
        {
            PrepareArrays();

            Socket.ExpectConnected(() => true);
            Socket.ExpectSend(data => {
                Assert.Equal(1000, data.Length);
                return 0;
            });

            Socket.ExpectConnected(() => true);

            Socket.ExpectConnected(() => true);

            Socket.ExpectConnected(() => true);
            
            Socket.ExpectConnected(() => true);
            
            Socket.ExpectClose();

            Socket.ExpectConnected(() => false);

            Assert.True(_cc.TryWrite(_bytes1K));
            Assert.True(_cc.TryWrite(_bytes1K));
            Assert.True(_cc.TryWrite(_bytes1K));
            Assert.True(_cc.TryWrite(_bytes996));
            Assert.False(_cc.TryWrite(_bytes10));
            Assert.False(_cc.TryWrite(_bytes4));

            Socket.ExpectAllDone();

            // No more sending possible
            Socket.ExpectConnected(() => false);

            _cc.Send();

            Socket.ExpectAllDone();
        }

        [Fact]
        public void ReadOutMessagePartiallyReceived()
        {
            // Console.WriteLine($"{20:X16}");

            // We say 10 bytes are available, but the length of the message will say 100
            Socket.ExpectConnected(() => true);
            
            Socket.ExpectAvailable(() => 10);

            Socket.ExpectReceive(data => {
                data[0] = 0x64;
                return 10;
            });

            Assert.Equal(0, _cc.ReceiveNext().Length);

            Socket.ExpectConnected(() => true);
            Socket.ExpectAvailable(() => 100);

            Socket.ExpectReceive(data =>
            {
                data[90] = 0x14;
                return 100;
            });

            Assert.Equal(100, _cc.ReceiveNext().Length);

            Socket.ExpectConnected(() => true);
            Socket.ExpectAvailable(() => 10);

            Socket.ExpectReceive(data => 10);

            Assert.Equal(20, _cc.ReceiveNext().Length);

            Socket.ExpectConnected(() => true);
            Socket.ExpectAvailable(() => 0);

            Assert.Equal(0, _cc.ReceiveNext().Length);

            Socket.ExpectAllDone();
        }

        [Fact]
        public void FillInbox()
        {
            // Console.WriteLine($"{1000:X16}");
            // Console.WriteLine($"{998:X16}");
            // Console.WriteLine($"{10:X16}");

            Socket.ExpectConnected(() => true);
            Socket.ExpectAvailable(() => 1000);

            Socket.ExpectReceive(data => {
                data.Clear();
                data[0] = 0xE8;
                data[1] = 0x03;
                return 1000;
            });

            Assert.Equal(1000, _cc.ReceiveNext().Length); 

            Socket.ExpectConnected(() => true);
            Socket.ExpectAvailable(() => 998);

            Socket.ExpectReceive(data => {
                data.Clear();
                data[0] = 0xE6;
                data[1] = 0x03;
                return 998;
            });

            Assert.Equal(998, _cc.ReceiveNext().Length);

            Socket.ExpectConnected(() => true);
            Socket.ExpectAvailable(() => 10);

            // Next message of size 17 is split in 2, simulating network issues
            Socket.ExpectReceive(data => {
                data.Clear();
                data[0] = 17;
                data[1] = 0;
                return 2;
            });

            Assert.Equal(0, _cc.ReceiveNext().Length);

            Socket.ExpectConnected(() => true);
            Socket.ExpectAvailable(() => 8);

            Socket.ExpectReceive(data => {
                data.Clear();
                return 15;
            });

            Assert.Equal(17, _cc.ReceiveNext().Length);

            // Receive multiple messages in one batch
            Socket.ExpectConnected(() => true);
            Socket.ExpectAvailable(() => 110);

            Socket.ExpectReceive(data => {
                data.Clear();
                data[0] = 100;
                data[1] = 0;

                data[100] = 17;
                data[101] = 00;
                return 117; // get 117 bytes in one Receive
            });

            Assert.Equal(100, _cc.ReceiveNext().Length);

            Socket.ExpectConnected(() => true);
            Socket.ExpectAvailable(() => 0);

            Assert.Equal(17, _cc.ReceiveNext().Length);

            Socket.ExpectConnected(() => true);
            Socket.ExpectAvailable(() => 0);

            Assert.Equal(0, _cc.ReceiveNext().Length);

            Socket.ExpectAllDone();
        }

        [Fact]
        public void NoConnectionWhenReceiving()
        {
            Socket.ExpectConnected(() => false);

            _cc.ReceiveNext();

            Socket.ExpectAllDone();
        }

        [Fact]
        public void CorruptedData_MessageTooBig()
        {
            // Any data that is received and is bigger than the MaxSizeInBytes is considered corrupted.
            Socket.ExpectConnected(() => true);
            Socket.ExpectAvailable(() => 2000);

            Socket.ExpectReceive(data =>
            {
                BitConverter.GetBytes(ClientServerMessage.MaxSizeInBytes + 1).CopyTo(data.Slice(0, 4));
                return ClientServerMessage.MaxSizeInBytes;
            });

            Socket.ExpectConnected(() => true);
            Socket.ExpectSend(data => data.Length);
            Socket.ExpectClose();

            _cc.ReceiveNext();

            Socket.ExpectAllDone();
        }

        [Fact]
        public void CorruptedData_MessageTooSmall()
        {
            // Any data that is received and is bigger than the MaxSizeInBytes is considered corrupted.
            Socket.ExpectConnected(() => true);
            Socket.ExpectAvailable(() => 2000);

            Socket.ExpectReceive(data =>
            {
                BitConverter.GetBytes(ClientServerMessage.MinSizeInBytes - 1).CopyTo(data.Slice(0, 4));
                return ClientServerMessage.MinSizeInBytes-1;
            });

            Socket.ExpectConnected(() => true);
            Socket.ExpectSend(data => data.Length);
            Socket.ExpectClose();

            _cc.ReceiveNext();

            Socket.ExpectAllDone();
        }

        [Fact]
        public void MessageTooLongToSend()
        {
            Socket.ExpectConnected(() => true);

            byte[] tooBig = new byte[ClientServerMessage.MaxSizeInBytes + 1];
            BitConverter.GetBytes(ClientServerMessage.MaxSizeInBytes + 1).CopyTo(new Span<byte>(tooBig, 0, 4));
            Assert.Throws<ArgumentException>(() => _cc.TryWrite(tooBig));

            Socket.ExpectAllDone();
        }

        [Fact]
        public void MessageWrongLengthIndicated()
        {
            Socket.ExpectConnected(() => true);

            byte[] wrongSize = new byte[ClientServerMessage.MaxSizeInBytes / 2];
            BitConverter.GetBytes(ClientServerMessage.MaxSizeInBytes / 2 + 2).CopyTo(new Span<byte>(wrongSize, 0, 4));
            Assert.Throws<ArgumentException>(() => _cc.TryWrite(wrongSize));

            Socket.ExpectAllDone();
        }

        [Fact]
        public void NoConnectionWhenWriting()
        {
            Socket.ExpectConnected(() => false);

            _cc.TryWrite(_bytes10);

            Socket.ExpectAllDone();
        }
    }
}
