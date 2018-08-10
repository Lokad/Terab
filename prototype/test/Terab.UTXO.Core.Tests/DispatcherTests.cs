using System;
using System.Collections.Concurrent;
using Terab.UTXO.Core.Messaging;
using Terab.UTXO.Core.Networking;
using Xunit;

namespace Terab.UTXO.Core.Tests
{
    public class DispatcherTests
    {
        private const int LargeMessageSize = ClientServerMessage.MaxSizeInBytes / 2;

        [Fact]
        public void FiveInboxes()
        {
            Assert.Throws<ArgumentException>(() =>
                new Dispatcher(new ConcurrentQueue<ClientConnection>(), 3, BoundedInbox.Create(), new BoundedInbox[5],
                    BoundedInbox.Create()));
        }

        [Fact]
        public void SevenInboxes()
        {
            Assert.Throws<ArgumentException>(() =>
                new Dispatcher(new ConcurrentQueue<ClientConnection>(), 3, BoundedInbox.Create(), new BoundedInbox[7],
                    BoundedInbox.Create()));
        }

        [Fact]
        public void ZeroInboxes()
        {
            Assert.Throws<ArgumentException>(() =>
                new Dispatcher(new ConcurrentQueue<ClientConnection>(), 3, BoundedInbox.Create(), new BoundedInbox[0],
                    BoundedInbox.Create()));
        }

        [Fact]
        public void FourInboxes()
        {
            new Dispatcher(new ConcurrentQueue<ClientConnection>(), 3, BoundedInbox.Create(), new BoundedInbox[4],
                BoundedInbox.Create());
        }

        [Fact]
        public void SixtyFourInboxes()
        {
            new Dispatcher(new ConcurrentQueue<ClientConnection>(), 3, BoundedInbox.Create(), new BoundedInbox[64],
                BoundedInbox.Create());
        }

        [Fact]
        public void QueueTooManyClients()
        {
            var queue = new ConcurrentQueue<ClientConnection>();
            var socket = new MockSocket();
            var client0 = new ClientConnection(socket, ClientId.Next(), 100);
            var client1 = new ClientConnection(socket, ClientId.Next(), 100);
            var client2 = new ClientConnection(socket, ClientId.Next(), 100);
            var client3 = new ClientConnection(socket, ClientId.Next(), 100);
            socket.ExpectConnected(() => true);
            socket.ExpectSend(data =>
            {
                Console.WriteLine(BitConverter.ToString(data.ToArray()));
                Console.WriteLine(BitConverter.ToString(MessageCreationHelper.NoMoreSpaceForClientsMessage));
                Assert.Equal(MessageCreationHelper.NoMoreSpaceForClientsMessage, data.ToArray());
                return data.Length;
            });
            socket.ExpectClose();

            var dispatcher = new Dispatcher(queue, 3, BoundedInbox.Create(), new BoundedInbox[64],
                BoundedInbox.Create());
            queue.Enqueue(client0);
            queue.Enqueue(client1);
            queue.Enqueue(client2);
            queue.Enqueue(client3);
            dispatcher.DequeueNewClients();

            socket.ExpectAllDone();
        }

        [Fact]
        public void RemoveDeadClients()
        {
            var queue = new ConcurrentQueue<ClientConnection>();
            var socket = new MockSocket();
            var client = new ClientConnection(socket, ClientId.Next(), 100);
            // First normal loop
            socket.ExpectConnected(() => true);
            socket.ExpectConnected(() => true);
            socket.ExpectConnected(() => true);
            socket.ExpectAvailable(() => 0);
            // Second loop where client is disconnected and therefore deleted
            socket.ExpectConnected(() => false);
            // No actions for the third loop which should be empty

            var dispatcher = new Dispatcher(queue, 3, BoundedInbox.Create(), new BoundedInbox[64],
                BoundedInbox.Create());
            queue.Enqueue(client);
            dispatcher.DequeueNewClients();
            // Normal listen (nothing happens though)
            dispatcher.ListenToConnections();
            // Client is disconnected
            dispatcher.ListenToConnections();
            // Empty loop
            dispatcher.ListenToConnections();

            socket.ExpectAllDone();
        }

        [Fact]
        public void FillControllerInbox()
        {
            var queue = new ConcurrentQueue<ClientConnection>();
            var socket = new MockSocket();
            var client1 = new ClientConnection(socket, ClientId.Next(), 100);
            var client2 = new ClientConnection(socket, ClientId.Next(), 100);
            

            // Client1 empty inbox
            socket.ExpectConnected(() => true);
            socket.ExpectConnected(() => true);
            socket.ExpectConnected(() => true);
            socket.ExpectAvailable(() => 0);
            // Client2 unsharded message
            socket.ExpectConnected(() => true);
            socket.ExpectConnected(() => true);
            socket.ExpectConnected(() => true);
            socket.ExpectAvailable(() => LargeMessageSize);
            socket.ExpectReceive(data =>
            {
                data.Clear();
                BitConverter.GetBytes(LargeMessageSize).CopyTo(data);
                return LargeMessageSize;
            });
            socket.ExpectConnected(() => true);
            socket.ExpectAvailable(() => 0);

            // Client1 unsharded message, fills up ControllerInbox
            socket.ExpectConnected(() => true);
            socket.ExpectConnected(() => true);
            socket.ExpectConnected(() => true);
            socket.ExpectAvailable(() => LargeMessageSize);
            socket.ExpectReceive(data =>
            {
                data.Clear();
                BitConverter.GetBytes(LargeMessageSize).CopyTo(data);
                return LargeMessageSize;
            });
            socket.ExpectConnected(() => true);
            socket.ExpectAvailable(() => 0);

            // Client2 unsharded message, gets BufferFull response
            socket.ExpectConnected(() => true);
            socket.ExpectConnected(() => true);
            socket.ExpectConnected(() => true);
            socket.ExpectAvailable(() => ClientServerMessage.PayloadStart); // minimal valid message size
            socket.ExpectReceive(data =>
            {
                data.Clear();
                BitConverter.GetBytes(ClientServerMessage.PayloadStart).CopyTo(data);
                return ClientServerMessage.PayloadStart;
            });
            socket.ExpectConnected(() => true);
            socket.ExpectSend(data =>
            {
                Assert.Equal(ClientServerMessage.PayloadStart, data.Length);
                Assert.Equal(data[13], (int) MessageType.ServerBusy);
                return ClientServerMessage.PayloadStart;
            });
            socket.ExpectConnected(() => true);
            socket.ExpectAvailable(() => 0);

            var dispatcher = new Dispatcher(queue, 3, BoundedInbox.Create(), new BoundedInbox[64],
                BoundedInbox.Create());
            queue.Enqueue(client1);
            queue.Enqueue(client2);
            dispatcher.DequeueNewClients();
            // Client1 empty inbox, Client2 LargeMessageSize bytes unsharded
            dispatcher.ListenToConnections();
            // Client1 LargeMessageSize bytes unsharded, Client2 10 bytes, error message
            dispatcher.ListenToConnections();

            socket.ExpectAllDone();
        }

        [Fact]
        public void FillShardedInboxes()
        {
            var queue = new ConcurrentQueue<ClientConnection>();
            var socket = new MockSocket();
            var client1 = new ClientConnection(socket, ClientId.Next(), 100);
            var client2 = new ClientConnection(socket, ClientId.Next(), 100);
            BoundedInbox[] threadInboxes =
            {
                BoundedInbox.Create(), BoundedInbox.Create()
            };

            var dispatcher = new Dispatcher(queue, 3, BoundedInbox.Create(),
                threadInboxes, BoundedInbox.Create());
            // Client1 sharded message
            socket.ExpectConnected(() => true);
            socket.ExpectConnected(() => true);
            socket.ExpectConnected(() => true);
            socket.ExpectAvailable(() => LargeMessageSize);
            socket.ExpectReceive(data =>
            {
                data.Clear();
                BitConverter.GetBytes(LargeMessageSize).CopyTo(data);
                data[ClientServerMessage.ShardedIndStart] = 0xFF; // 0xFF: all bits set to one, so "sharded" is set to 1 for sure
                data[ClientServerMessage.PayloadStart] = 0x01;
                return LargeMessageSize;
            });
            socket.ExpectConnected(() => true);
            socket.ExpectAvailable(() => 0);

            // Client2 sharded message
            socket.ExpectConnected(() => true);
            socket.ExpectConnected(() => true);
            socket.ExpectConnected(() => true);
            socket.ExpectAvailable(() => LargeMessageSize);
            socket.ExpectReceive(data =>
            {
                data.Clear();
                BitConverter.GetBytes(LargeMessageSize).CopyTo(data);
                data[12] = 0xFF;
                return LargeMessageSize;
            });
            socket.ExpectConnected(() => true);
            socket.ExpectAvailable(() => 0);

            // Client1 sharded message, fills up first sharded inbox
            socket.ExpectConnected(() => true);
            socket.ExpectConnected(() => true);
            socket.ExpectConnected(() => true);
            socket.ExpectAvailable(() => LargeMessageSize);
            socket.ExpectReceive(data =>
            {
                data.Clear();
                BitConverter.GetBytes(LargeMessageSize).CopyTo(data);
                data[ClientServerMessage.ShardedIndStart] = 0xFF; // 0xFF: all bits set to one, so "sharded" is set to 1 for sure
                data[ClientServerMessage.PayloadStart] = 0x00;
                return LargeMessageSize;
            });
            socket.ExpectConnected(() => true);
            socket.ExpectAvailable(() => 0);

            // Client2 sharded message, fills up second sharded inbox
            socket.ExpectConnected(() => true);
            socket.ExpectConnected(() => true);
            socket.ExpectConnected(() => true);
            socket.ExpectAvailable(() => LargeMessageSize);
            socket.ExpectReceive(data =>
            {
                data.Clear();
                BitConverter.GetBytes(LargeMessageSize).CopyTo(data);
                data[ClientServerMessage.ShardedIndStart] = 0xFF; // 0xFF: all bits set to one, so "sharded" is set to 1 for sure
                data[ClientServerMessage.PayloadStart] = 0x01;
                return LargeMessageSize;
            });
            socket.ExpectConnected(() => true);
            socket.ExpectAvailable(() => 0);

            // Read and get error message server busy writing into second sharded inbox
            socket.ExpectConnected(() => true);
            socket.ExpectConnected(() => true);
            socket.ExpectConnected(() => true);
            socket.ExpectAvailable(() => 20);
            socket.ExpectReceive(data =>
            {
                data.Clear();
                BitConverter.GetBytes(20).CopyTo(data);
                data[ClientServerMessage.ShardedIndStart] = 0xFF;
                data[ClientServerMessage.PayloadStart] = 0x01;
                return 20;
            });
            socket.ExpectConnected(() => true);
            socket.ExpectSend(data =>
            {
                Assert.Equal(ClientServerMessage.PayloadStart, data.Length);
                Assert.Equal(data[13], (byte) MessageType.ServerBusy);
                return ClientServerMessage.PayloadStart;
            });
            socket.ExpectConnected(() => true);
            socket.ExpectAvailable(() => 0);

            // Read and get error message server busy writing into first sharded inbox
            socket.ExpectConnected(() => true);
            socket.ExpectConnected(() => true);
            socket.ExpectConnected(() => true);
            socket.ExpectAvailable(() => 20);
            socket.ExpectReceive(data =>
            {
                data.Clear();
                BitConverter.GetBytes(20).CopyTo(data);
                data[ClientServerMessage.ShardedIndStart] = 0xFF; // 0xFF: all bits set to one, so "sharded" is set to 1 for sure
                data[ClientServerMessage.PayloadStart] = 0x00;
                return 20;
            });
            socket.ExpectConnected(() => true);
            socket.ExpectSend(data =>
            {
                Assert.Equal(ClientServerMessage.PayloadStart, data.Length);
                Assert.Equal(MessageType.ServerBusy, ClientServerMessage.GetMessageType(data));
                return ClientServerMessage.PayloadStart;
            });
            socket.ExpectConnected(() => true);
            socket.ExpectAvailable(() => 0);

            queue.Enqueue(client1);
            queue.Enqueue(client2);
            dispatcher.DequeueNewClients();
            // Client1 into sharded inbox 1, Client2 into sharded inbox 2
            dispatcher.ListenToConnections();
            // Client1 filling up sharded inbox 2, Client2 filling up sharded inbox 1
            dispatcher.ListenToConnections();
            // Errors when trying to write into any inbox
            dispatcher.ListenToConnections();

            socket.ExpectAllDone();
        }

        [Fact]
        public void SendAnswers()
        {
            var queue = new ConcurrentQueue<ClientConnection>();
            var socket = new MockSocket();
            var outbox = BoundedInbox.Create();
            var client1 = new ClientConnection(socket, ClientId.Next(), 100);
            var client2 = new ClientConnection(socket, ClientId.Next(), 100);
            queue.Enqueue(client1);
            queue.Enqueue(client2);

            var dispatcher = new Dispatcher(queue, 3, outbox,
                new BoundedInbox[2], BoundedInbox.Create());

            // Try sending message to one client, doesn't succeed
            socket.ExpectConnected(() => true);
            socket.ExpectSend(data => 0);
            // Try sending message to the other client, doesn't succeed
            socket.ExpectConnected(() => true);
            socket.ExpectSend(data => 0);
            // Try sending message to first client, socket gets closed
            socket.ExpectConnected(() => true);
            socket.ExpectClose();
            // Try sending message to second client, socket gets closed
            socket.ExpectConnected(() => true);
            socket.ExpectClose();

            var firstMessage = new byte[100];
            BitConverter.GetBytes(100).CopyTo(new Span<byte>(firstMessage));
            client2.ConnectionId.WriteTo(  // send back to client2
                firstMessage.AsSpan(ClientServerMessage.ClientIdStart, ClientServerMessage.ClientIdSizeInBytes));

            var secondMessage = new byte[100];
            BitConverter.GetBytes(100).CopyTo(new Span<byte>(secondMessage));
            client1.ConnectionId.WriteTo( // send back to client1
                secondMessage.AsSpan(ClientServerMessage.ClientIdStart, ClientServerMessage.ClientIdSizeInBytes));

            var thirdMessage = new byte[50];
            BitConverter.GetBytes(50).CopyTo(new Span<byte>(thirdMessage));
            client1.ConnectionId.WriteTo( // send back to client1
                thirdMessage.AsSpan(ClientServerMessage.ClientIdStart, ClientServerMessage.ClientIdSizeInBytes));

            var fourthMessage = new byte[50];
            BitConverter.GetBytes(50).CopyTo(new Span<byte>(fourthMessage));
            client2.ConnectionId.WriteTo( // send back to client2
                fourthMessage.AsSpan(ClientServerMessage.ClientIdStart, ClientServerMessage.ClientIdSizeInBytes));

            // Write messages into dispatcher buffer for both clients
            Assert.True(outbox.TryWrite(firstMessage));
            Assert.True(outbox.TryWrite(secondMessage));
            dispatcher.DequeueNewClients();
            // Try sending the answers, fails
            dispatcher.SendResponses();

            Assert.True(outbox.TryWrite(thirdMessage));
            Assert.True(outbox.TryWrite(fourthMessage));
            dispatcher.SendResponses();

            socket.ExpectAllDone();
        }

        [Fact]
        public void ReadMultipleMessagesFromClient()
        {
            var queue = new ConcurrentQueue<ClientConnection>();
            var socket = new MockSocket();
            var client1 = new ClientConnection(socket, ClientId.Next(), 100);
            var client2 = new ClientConnection(socket, ClientId.Next(), 100);

            var dispatcher = new Dispatcher(queue, 3, BoundedInbox.Create(),
                new BoundedInbox[2], BoundedInbox.Create());
            // Client1 messages
            socket.ExpectConnected(() => true);
            socket.ExpectConnected(() => true);
            socket.ExpectConnected(() => true);
            socket.ExpectAvailable(() => 600);
            socket.ExpectReceive(data =>
            {
                data.Clear();
                // Three messages, of sizes 200, 300 and 100
                BitConverter.GetBytes(200).CopyTo(data);
                BitConverter.GetBytes(300).CopyTo(data.Slice(200));
                BitConverter.GetBytes(100).CopyTo(data.Slice(500));
                return 600;
            });
            // Reading second message
            socket.ExpectConnected(() => true);
            socket.ExpectAvailable(() => 0);
            // Reading third message
            socket.ExpectConnected(() => true);
            socket.ExpectAvailable(() => 0);
            // Exiting loop
            socket.ExpectConnected(() => true);
            socket.ExpectAvailable(() => 0);

            // Client2 messages
            socket.ExpectConnected(() => true);
            socket.ExpectConnected(() => true);
            socket.ExpectConnected(() => true);
            socket.ExpectAvailable(() => 100);
            socket.ExpectReceive(data =>
            {
                data.Clear();
                BitConverter.GetBytes(70).CopyTo(data);
                BitConverter.GetBytes(30).CopyTo(data.Slice(70));
                return 100;
            });
            // Reading second message
            socket.ExpectConnected(() => true);
            socket.ExpectAvailable(() => 0);
            // Exiting loop
            socket.ExpectConnected(() => true);
            socket.ExpectAvailable(() => 0);

            // Some more messages for Client1
            socket.ExpectConnected(() => true);
            socket.ExpectConnected(() => true);
            socket.ExpectConnected(() => true);
            socket.ExpectAvailable(() => 60);
            socket.ExpectReceive(data =>
            {
                data.Clear();
                // Two messages, of sizes 10 and 40
                BitConverter.GetBytes(25).CopyTo(data);
                BitConverter.GetBytes(35).CopyTo(data.Slice(25));
                return 60;
            });
            // Reading second message
            socket.ExpectConnected(() => true);
            socket.ExpectAvailable(() => 0);
            // Exiting loop
            socket.ExpectConnected(() => true);
            socket.ExpectAvailable(() => 0);

            // No more messages for Client2
            socket.ExpectConnected(() => true);
            socket.ExpectConnected(() => true);
            socket.ExpectConnected(() => true);
            socket.ExpectAvailable(() => 0);

            queue.Enqueue(client1);
            queue.Enqueue(client2);
            dispatcher.DequeueNewClients();
            // Client1 reading 3 messages, Client2 two
            dispatcher.ListenToConnections();
            // Client1 reading 2 more messages, Client2 none
            dispatcher.ListenToConnections();

            socket.ExpectAllDone();
        }
    }
}