// Copyright Lokad 2018 under MIT BCH.
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Terab.Lib.Messaging;

namespace Terab.Lib.Networking
{
    /// <summary>
    /// Listens to incoming client connections and, if there are any, adds them to a shared
    /// thread safe queue.  Other threads would scan the queue to proceed further with those
    /// new client connections.
    /// </summary>
    public class Listener
    {
        private readonly Socket _socket;

        private readonly BoundedInbox _dispatchInbox;

        private readonly ILog _log;

        /// <summary> Intended to facilitate unit testing. </summary>
        internal Func<ClientId> GetNextClientId { get; set; } = ClientId.Next;

        public Action<ConnectionController> OnConnectionAccepted { get; set; }

        public int Port { get; private set; }

        /// <remarks>
        /// A port at zero indicates to chose any available port.
        /// </remarks>
        public Listener(
            BoundedInbox dispatchInbox,
            IPAddress ipAddress,
            int port,
            ILog log = null)
        {
            _dispatchInbox = dispatchInbox;
            _log = log;

            var localEndPoint = new IPEndPoint(ipAddress, port);

            _socket = new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            _socket.Bind(localEndPoint);

            Port = ((IPEndPoint) _socket.LocalEndPoint).Port;

            _socket.Listen(Constants.SocketReceiveBufferSize);
        }

        public void Loop(CancellationToken cancel)
        {
            if (OnConnectionAccepted == null)
                throw new InvalidOperationException("OnConnectionAccepted is null.");

            _log?.Log(LogSeverity.Info, $"Listener started on port {Port}.");
            try
            {
                while (!cancel.IsCancellationRequested)
                {
                    try
                    {
                        var acceptTask = _socket.AcceptAsync();
                        acceptTask.Wait(cancel);

                        Socket s = acceptTask.GetAwaiter().GetResult();

                        var connection = new ConnectionController(
                            _dispatchInbox,
                            new SocketLikeAdapter(s),
                            GetNextClientId(),
                            _log);

                        // Wake the dispatch controller.
                        OnConnectionAccepted(connection);
                    }
                    catch (OperationCanceledException oce) when (oce.CancellationToken.Equals(cancel))
                    {
                        break;
                    }
                    catch (SocketException)
                    {
                        break;
                    }
                }
            }
            finally
            {
                _socket.Close();
            }

            _log?.Log(LogSeverity.Info, $"Listener on port {Port} exited.");
        }
    }
}