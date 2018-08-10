using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Terab.UTXO.Core.Messaging;

namespace Terab.UTXO.Core.Networking
{ 
    // TODO: [vermorel] Clarify the intent with a comment: who is listening to what? why?
    public class Listener
    {
        private readonly Socket _listener;

        private readonly ConcurrentQueue<ClientConnection> _queue;

        private readonly int _bufferOutLength;

        public Listener(ConcurrentQueue<ClientConnection> queue, Configuration config)
        {
            _bufferOutLength = config.SocketSendBufferSize;
            _queue = queue;

            var localEndPoint = new IPEndPoint(config.ListenAddress, config.ListenPort);
            _listener = new Socket(config.ListenAddress.AddressFamily,
                SocketType.Stream, ProtocolType.Tcp)
            {
                Blocking = false
            };
            _listener.Bind(localEndPoint);
            _listener.Listen(config.ListenBacklogSize);
        }

        public void Listen(CancellationToken cancel)
        {
            // TODO: [vermorel] Use a 'ConnectionId' instead.
            cancel.Register(() => _listener.Close());
            while (!cancel.IsCancellationRequested)
            {
                try
                {
                    var s =_listener.Accept();
                    _queue.Append(new ClientConnection(s, ClientId.Next(), _bufferOutLength));
                }
                catch
                {
                    // TODO: [vermorel] We must log exceptions here.
                    // TODO: [vermorel] In debug mode, I would still suggest to crash the server.

                    // An exception is not allowed to kill the accept loop
                }
            }
        }
    }
}
