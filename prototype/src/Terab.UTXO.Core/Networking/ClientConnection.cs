using System;
using System.Net.Sockets;
using Terab.UTXO.Core.Messaging;

namespace Terab.UTXO.Core.Networking
{
    /// <summary>
    /// Represents the connection with one Terab client via a socket.
    /// </summary>
    /// <remarks>
    /// This class mainly provides some additional buffers to handle information
    /// received from and to be sent to the client. It merely does the relay
    /// between the socket and the <see cref="Dispatcher"/>.
    /// </remarks>
    public sealed class ClientConnection
    {
        private readonly ISocketLike _socket;

        private readonly byte[] _bufferIn;

        private readonly byte[] _bufferOut;

        // TODO: [vermorel] #34 introduce a dedicated struct 'ConnectionId'.

        /// <summary>
        /// ID of the client connected to that socket.
        /// </summary>
        public ClientId ConnectionId { get; }

        private int _endIn;

        // no _startOut or _startIn needed because bytes get copied to
        // beginning of buffer once things were sent/read

        private int _endOut;

        public bool IsConnected => _socket.Connected();

        // TODO: [vermorel] using something named 'PayloadStart' for the array size look incorrect
        private readonly byte[] _errorSpan = new byte[ClientServerMessage.PayloadStart];

        public ClientConnection(ISocketLike socket, ClientId connectionId, int bufferOutInBytes)
        {
            if (!connectionId.IsSpecified)
            {
                throw new ArgumentException("connectionId not specified", nameof(connectionId));
            }

            _socket = socket;
            ConnectionId = connectionId;
            _bufferIn = new byte[ClientServerMessage.MaxSizeInBytes];
            _bufferOut = new byte[bufferOutInBytes];
        }

        public ClientConnection(Socket s, ClientId connectionId, int bufferOutInBytes)
            : this(new SocketLikeAdapter(s), connectionId, bufferOutInBytes) {}

        // TODO: [vermorel] Should probably be renamed 'GetInputMessageLenght()'.
        private int InputMessageLength()
        {
            if (_endIn < sizeof(int))
                return 0;

            if (!ClientServerMessage.TryGetLength(_bufferIn, out var length))
            {
                // TODO: [vermorel] Pattern below is bizarelly written, to be rewritten.
                new Span<byte>(_errorSpan).EmitRequestTooShort(_bufferIn);

                if (_socket.Connected())
                {
                    _socket.Send(_errorSpan);
                    _socket.Close();
                }

                return 0;
            }

            if (length > ClientServerMessage.MaxSizeInBytes || length < ClientServerMessage.MinSizeInBytes)
            {
                // TODO: [vermorel] Pattern below is bizarelly written, to be rewritten.
                new Span<byte>(_errorSpan).EmitRequestTooLong(_bufferIn);

                if (_socket.Connected())
                {
                    _socket.Send(_errorSpan);
                    _socket.Close();
                }

                return 0;
            }

            // TODO: [vermorel] Clarify what is the edge-case here. Unclear.
            if (_endIn < length)
                return 0;

            return length;
        }

        // TODO: [vermorel] Almost but not quite like other 'Iterator' in solution. Align designs.

        /// <summary>
        /// Provides the next unread message.
        /// </summary>
        /// <remarks> 
        /// Returned span is only valid until the next call to 
        /// <see cref="ReceiveNext"/>.
        /// </remarks>        
        public Span<byte> ReceiveNext()
        {
            // Delete first message and copy everything else to the beginning
            var length = InputMessageLength();
            if (length > 0)
            {
                Array.Copy(_bufferIn, length, _bufferIn, 0, _endIn - length);
                _endIn -= length;
            }

            // Try to receive more data from socket
            if (IsConnected && 0 != _socket.Available() && _bufferIn.Length != _endIn)
            {
                _endIn += _socket.Receive(new Span<byte>(_bufferIn).Slice(_endIn));
            }

            length = InputMessageLength();
            if (length > 0)
            {
                var message = new Span<byte>(_bufferIn, 0, length);

                var messageStatus = PassesVerifications(message);

                if (messageStatus != MessageType.EverythingOk)
                {
                    switch (messageStatus)
                    {
                        case MessageType.ClientIdFieldNotEmpty:
                            new Span<byte>(_errorSpan).EmitClientIdFieldNotEmpty(message);
                            _socket.Send(_errorSpan);
                            // see whether a valid message is waiting in the inbox
                            return ReceiveNext();
                    }
                }
                else // write client ID into message
                {
                    ConnectionId.WriteTo(message.Slice(8));
                }
                return new Span<byte>(_bufferIn, 0, length);
            }
            else
            {
                return Span<byte>.Empty;
            }

            // TODO: [vermorel] The intent of this method must be clarified.
            MessageType PassesVerifications(Span<byte> message)
            {
                // Maybe other verifications are going to be added over time
                if (message.Length >= ClientServerMessage.PayloadStart && ClientServerMessage.GetClientId(message).IsSpecified)
                {
                    return MessageType.ClientIdFieldNotEmpty;
                }

                return MessageType.EverythingOk;
            }
        }

        // TODO: [vermorel] The condition for not succeeding to write should be clarified.
        public bool TryWrite(Span<byte> message)
        {
            // TODO: [vermorel] If we are not even connected, we should throw an 'InvalidOperationException'.
            if (!IsConnected)
            {
                return false;
            }

            if (message.Length > ClientServerMessage.MaxSizeInBytes)
                throw new ArgumentException(
                    $"Message size {message.Length} exceeds max size {ClientServerMessage.MaxSizeInBytes}.");

            ClientServerMessage.TryGetLength(message, out var announcedMessageLength);

            if (announcedMessageLength != message.Length)
                throw new ArgumentException(
                    $"Message size {announcedMessageLength} in span of size {message.Length}.");

            // TODO: [vermorel] This operation should be moving within the 'ClientServerMessage'
            // delete client ID which is just for internal purposes
            BitConverter.TryWriteBytes(message.Slice(8), 0U);

            // Try to send message only if buffer is empty, to not corrupt the message stream
            if (_endOut > 0)
            {
                if (message.Length > _bufferOut.Length - _endOut)
                {
                    // No room in buffer for this message, close connection.
                    // This behaviour may change in future versions.
                    _socket.Close();
                    return false;
                }

                if (!message.TryCopyTo(new Span<byte>(_bufferOut).Slice(_endOut)))
                    // Should really replace this with a span copy function
                    throw new NotImplementedException("No handling for case when byte copy fails");

                _endOut += message.Length;

                return true;
            }

            // Nothing in buffer, so the socket is free for sending
            var bytesSent = _socket.Send(message);

            if (bytesSent == message.Length)
                return true;

            var bytesNotSent = message.Length - bytesSent;

            if (!message.Slice(bytesSent).TryCopyTo(_bufferOut))
                // TODO: Should really replace this with a span copy function
                throw new NotImplementedException("No handling for case when byte copy fails");

            _endOut += bytesNotSent;
            return true;
        }

        /// <summary>
        /// Send to the socket any unsent data passed to <see cref="TryWrite"/>.
        /// </summary>
        /// <remarks>
        /// Multiple calls may be necessary before all data is sent out.
        /// </remarks>
        public void Send()
        {
            // TODO: [vermorel] Throw exception instead, don't silently return.
            if (!IsConnected)
                return;

            // Try to send
            if (_endOut != 0)
            {
                var bytesSent = _socket.Send(new Span<byte>(_bufferOut, 0, _endOut));
                if (bytesSent != _endOut)
                {
                    // not all bytes have been sent
                    Array.Copy(_bufferIn, bytesSent, _bufferIn, 0, _endOut - bytesSent);
                }
                else _endOut = 0; // free the whole buffer   
            }
        }

        public void Close() => _socket.Close();

        private class SocketLikeAdapter : ISocketLike
        {
            private readonly Socket _socket;

            public SocketLikeAdapter(Socket s)
            {
                _socket = s;
            }

            public int Available()
            {
                return _socket.Available;
            }

            public int Send(Span<byte> toSend)
            {
                // TODO: make span asap
                return _socket.Send(toSend.ToArray());
            }

            public int Receive(Span<byte> bufferIn)
            {
                // TODO: adapt to span capable API asap
                var received = new byte[bufferIn.Length];
                var numBytesReceived = _socket.Receive(received);
                received.CopyTo(bufferIn);
                return numBytesReceived;
            }

            public bool Connected()
            {
                return _socket.Connected;
            }

            public void Close()
            {
                _socket.Close();
            }
        }
    }
}