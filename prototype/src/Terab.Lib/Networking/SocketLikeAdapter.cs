// Copyright Lokad 2018 under MIT BCH.
using System;
using System.Net.Sockets;

namespace Terab.Lib.Networking
{
    /// <summary> Adapter around 'Socket'. Facilitate send/receive messages
    /// of known sizes. Facilitate unit testing as well. </summary>
    public class SocketLikeAdapter : ISocketLike
    {
        private readonly Socket _socket;

        public SocketLikeAdapter(Socket s)
        {
            _socket = s;

            // Socket delays can cause very large overhead for an app like Terab, bandwidth is a non-issue.
            _socket.NoDelay = true;

            _socket.SendTimeout = Constants.SocketSendTimeoutMs;
            _socket.SendBufferSize = Constants.SocketSendBufferSize;
            _socket.ReceiveBufferSize = Constants.SocketReceiveBufferSize;
        }

        public int Available => _socket.Available;

        public bool Connected => _socket.Connected;

        public void Send(Span<byte> bufferOut)
        {
            do
            {
                var sent = _socket.Send(bufferOut);
                bufferOut = bufferOut.Slice(sent);

            } while (!bufferOut.IsEmpty);
        }

        public void Receive(Span<byte> bufferIn)
        {
            do
            {
                var received = _socket.Receive(bufferIn);
                bufferIn = bufferIn.Slice(received);
            } while (!bufferIn.IsEmpty);
        }

        public void Close()
        {
            _socket.Close();
        }
    }
}
