// Copyright Lokad 2018 under MIT BCH.
using System;
using System.Buffers.Binary;
using System.Linq;
using Terab.Lib.Messaging;
using Terab.Lib.Messaging.Protocol;
using Terab.Lib.Tests.Mock;
using Xunit;

namespace Terab.Lib.Tests
{
    public class DispatchControllerTests
    {
        private const int LargeMessageSize = Constants.MaxResponseSize / 2;

        [Fact]
        public void FourInboxes()
        {
            new DispatchController(new BoundedInbox(),
                new BoundedInbox(), new BoundedInbox[4], new IdentityHash());
        }

        [Fact]
        public void ZeroInboxes()
        {
            Assert.Throws<ArgumentException>(() =>
                new DispatchController(new BoundedInbox(), new BoundedInbox(), new BoundedInbox[0],
                    new IdentityHash()));
        }

        [Fact]
        public void QueueTooManyClients()
        {
            var socket = new MockSocket();
            var dispatcherInbox = new BoundedInbox();
            var dispatcher = new DispatchController(dispatcherInbox, new BoundedInbox(), new BoundedInbox[64],
                new IdentityHash());
            dispatcher.OnConnectionAccepted = (_) => { };

            for (var i = 0; i < Constants.MaxActiveConnections; i++)
            {
                var client0 = new ConnectionController(dispatcherInbox, socket, ClientId.Next());
                client0.OnRequestReceived = () => { };
                dispatcher.AddConnection(client0);
                dispatcher.HandleNewConnection();
            }

            var client1 = new ConnectionController(dispatcherInbox, socket, ClientId.Next());
            client1.OnRequestReceived = () => { };

            socket.ExpectSend(data =>
            {
                Assert.Equal(ProtocolErrorResponse.SizeInBytes, data.Length);

                var errorMessage = new ProtocolErrorResponse(data);
                Assert.Equal(MessageKind.ProtocolErrorResponse, errorMessage.MessageHeader.MessageKind);
                Assert.Equal(ProtocolErrorStatus.TooManyActiveClients, errorMessage.Status);

                return data.Length;
            });

            dispatcher.AddConnection(client1);
            dispatcher.HandleNewConnection();
            client1.HandleResponse();
            socket.ExpectAllDone();
        }

        [Fact]
        public void RemoveDeadClients()
        {
            var socket = new MockSocket();
            var dispatcherInbox = new BoundedInbox();
            var client = new ConnectionController(dispatcherInbox, socket, ClientId.Next());
            client.OnRequestReceived = () => { };

            socket.ExpectConnected(() => false);

            var dispatcher = new DispatchController(dispatcherInbox, new BoundedInbox(), new BoundedInbox[4],
                new IdentityHash());
            dispatcher.OnConnectionAccepted = (_) => { };
            dispatcher.AddConnection(client);
            dispatcher.HandleNewConnection();

            var firstMessage = new byte[100];
            var message = new Message(firstMessage);
            message.Header.ClientId = client.ClientId;
            message.Header.MessageSizeInBytes = 100;
            message.Header.MessageKind = MessageKind.CloseConnection;

            Assert.True(dispatcherInbox.TryWrite(firstMessage));

            dispatcher.HandleRequest();

            socket.ExpectAllDone();
        }

        [Fact(Skip = "TODO: missing upgrade with respect of removal of 'PingChainController'.")]
        public void FillChainControllerInbox()
        {
            var socket1 = new MockSocket();
            var socket2 = new MockSocket();
            var dispatcherInbox = new BoundedInbox();
            var client1 = new ConnectionController(dispatcherInbox, socket1, ClientId.Next());
            var client2 = new ConnectionController(dispatcherInbox, socket2, ClientId.Next());
            client1.OnRequestReceived = () => { };
            client2.OnRequestReceived = () => { };

            Func<int, int, MockSocket.SpanToInt> func = (s1, s2) =>
            {
                return data =>
                {
                    data.Clear();
                    BinaryPrimitives.TryWriteInt32LittleEndian(data, s1);
                    // TODO: [vermorel] PingChainController has been removed, logic need to be upgraded.
                    //MessageKind.PingChainController.WriteTo(data.Slice(MessageHeaderHelper.MessageKindStart));
                    return s2;
                };
            };

            socket2.ExpectReceive(func(LargeMessageSize, MessageHeader.SizeInBytes));
            socket2.ExpectReceive(func(LargeMessageSize, LargeMessageSize));
            socket1.ExpectReceive(func(LargeMessageSize, MessageHeader.SizeInBytes));
            socket1.ExpectReceive(func(LargeMessageSize, LargeMessageSize));
            // request too short
            var bodyStart = sizeof(int) + RequestId.SizeInBytes + ClientId.SizeInBytes + sizeof(MessageKind);
            socket2.ExpectReceive(func(bodyStart, bodyStart));


            socket2.ExpectSend(data =>
            {
                Assert.Equal(ProtocolErrorResponse.SizeInBytes, data.Length);
                var message = new ProtocolErrorResponse(data);
                Assert.Equal(MessageKind.ProtocolErrorResponse, message.MessageHeader.MessageKind);
                Assert.Equal(ProtocolErrorStatus.RequestTooShort, message.Status);

                return ProtocolErrorResponse.SizeInBytes;
            });

            socket2.ExpectConnected(() => true);
            socket1.ExpectConnected(() => true);

            var dispatcher = new DispatchController(dispatcherInbox,
                new BoundedInbox(Constants.MaxResponseSize),
                Enumerable.Range(0, 32).Select(x => new BoundedInbox()).ToArray(),
                new IdentityHash());

            // Nil handling of notifications
            dispatcher.OnBlockMessageDispatched = () => { };
            for (var i = 0; i < dispatcher.OnCoinMessageDispatched.Length; i++)
                dispatcher.OnCoinMessageDispatched[i] = () => { };

            dispatcher.OnConnectionAccepted = (_) => { };
            dispatcher.AddConnection(client1);
            dispatcher.AddConnection(client2);

            dispatcher.HandleNewConnection();
            dispatcher.HandleNewConnection();
            client1.HandleRequest();
            client2.HandleRequest();
            client2.HandleRequest();
            client2.HandleResponse();
            dispatcher.HandleRequest();
            dispatcher.HandleRequest();
            dispatcher.HandleRequest();

            socket1.ExpectAllDone();
            socket2.ExpectAllDone();
        }

        /// <summary>
        /// Test <see cref="ConnectionController"/> OutboundBuffer overflows
        /// and cause a connection close.
        /// </summary>
        [Fact]
        public void SendAnswers()
        {
            var dispatcherInbox = new BoundedInbox();
            var socket1 = new MockSocket();
            var socket2 = new MockSocket();
            var client1 = new ConnectionController(dispatcherInbox, socket1, ClientId.Next());
            var client2 = new ConnectionController(dispatcherInbox, socket2, ClientId.Next());
            client1.OnRequestReceived = () => { };
            client2.OnRequestReceived = () => { };

            var dispatcher = new DispatchController(dispatcherInbox, new BoundedInbox(), new BoundedInbox[2],
                new IdentityHash());
            // Nil handling of notifications
            dispatcher.OnBlockMessageDispatched = () => { };
            for (var i = 0; i < dispatcher.OnCoinMessageDispatched.Length; i++)
                dispatcher.OnCoinMessageDispatched[i] = () => { };
            dispatcher.OnConnectionAccepted = (_) => { };


            var firstMessage = new byte[Constants.MaxRequestSize];
            var message = new Message(firstMessage);
            message.Header.ClientId = client2.ClientId;
            message.Header.MessageSizeInBytes = Constants.MaxRequestSize;
            message.Header.MessageKind = MessageKind.GetCoinResponse;


            var secondMessage = new byte[Constants.MaxRequestSize];
            message = new Message(secondMessage);
            message.Header.ClientId = client1.ClientId;
            message.Header.MessageSizeInBytes = Constants.MaxRequestSize;
            message.Header.MessageKind = MessageKind.GetCoinResponse;

            var thirdMessage = new byte[50];
            message = new Message(thirdMessage);
            message.Header.ClientId = client1.ClientId;
            message.Header.MessageSizeInBytes = 50;
            message.Header.MessageKind = MessageKind.GetCoinResponse;


            var fourthMessage = new byte[50];
            message = new Message(fourthMessage);
            message.Header.ClientId = client2.ClientId;
            message.Header.MessageSizeInBytes = 50;
            message.Header.MessageKind = MessageKind.GetCoinResponse;

            dispatcher.AddConnection(client1);
            dispatcher.AddConnection(client2);
            dispatcher.HandleNewConnection();
            dispatcher.HandleNewConnection();


            // Write messages into dispatcher buffer for both clients
            for (var i = 0; i < Constants.SocketSendBufferSize / Constants.MaxRequestSize; i++)
            {
                socket1.ExpectConnected(() => true);
                socket2.ExpectConnected(() => true);

                Assert.True(dispatcherInbox.TryWrite(firstMessage));
                Assert.True(dispatcherInbox.TryWrite(secondMessage));

                // Try sending the answers, fails
                dispatcher.HandleRequest();
                dispatcher.HandleRequest();
            }

            // Try sending message to first client, socket gets closed
            socket1.ExpectConnected(() => true);
            socket1.ExpectClose();
            // Try sending message to second client, socket gets closed
            socket2.ExpectConnected(() => true);
            socket2.ExpectClose();

            Assert.True(dispatcherInbox.TryWrite(thirdMessage));
            Assert.True(dispatcherInbox.TryWrite(fourthMessage));
            dispatcher.HandleRequest();
            dispatcher.HandleRequest();

            socket1.ExpectAllDone();
            socket2.ExpectAllDone();
        }
    }
}