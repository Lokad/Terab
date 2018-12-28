// Copyright Lokad 2018 under MIT BCH.
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using Terab.Lib.Coins;
using Terab.Lib.Messaging;
using Terab.Lib.Messaging.Protocol;

namespace Terab.Lib
{
    /// <summary>
    /// Central communication manager and coordinator between the
    /// messages received by the Terab clients and the threads that are
    /// available to treat them. It is mono-threaded and the bottleneck through
    /// which any message will have to pass if it is to be treated by the Terab
    /// application. The responses returned by the threads also pass through
    /// here and are redirected to the corresponding
    /// <see cref="ConnectionController"/> which forwards back to the original
    /// client.
    /// </summary>
    public class DispatchController
    {
        private readonly ConcurrentQueue<ConnectionController> _queue;

        /// <summary>
        /// Active connections.
        /// </summary>
        private readonly Dictionary<ClientId, ConnectionController> _connections;

        /// <summary>
        /// Collects all the messages to be sorted by the dispatcher.
        /// </summary>
        private readonly BoundedInbox _inbox;

        private readonly BoundedInbox _chainControllerBox;

        private readonly BoundedInbox[] _coinControllerBoxes;

        /// <summary>
        /// Intended to route requests to the right coin controller,
        /// ensuring a load-balancing behavior.
        /// </summary>
        private readonly IOutpointHash _hash;

        private readonly ILog _log;

        private readonly ManualResetEvent _mre;

        /// <summary>
        /// Intended to call 'ConnectionController.Start()' with the
        /// possibility to intercept the call for testing purposes.
        /// </summary>
        public Action<ConnectionController> OnConnectionAccepted { get; set; }

        /// <summary>
        /// Intended to wake the block controller.
        /// </summary>
        public Action OnBlockMessageDispatched { get; set; }

        /// <summary>
        /// Intended to wake the coin controller.
        /// </summary>
        public Action[] OnCoinMessageDispatched { get; }

        public DispatchController(
            BoundedInbox inbox,
            BoundedInbox chainControllerBox,
            BoundedInbox[] coinControllerBoxes,
            IOutpointHash hash,
            ILog log = null)
        {
            if (coinControllerBoxes.Length == 0)
                throw new ArgumentException(nameof(coinControllerBoxes));

            _queue = new ConcurrentQueue<ConnectionController>();

            _inbox = inbox;

            _chainControllerBox = chainControllerBox;
            _coinControllerBoxes = coinControllerBoxes;
            _hash = hash;
            _log = log;

            _mre = new ManualResetEvent(false);

            OnCoinMessageDispatched = new Action[_coinControllerBoxes.Length];

            _connections = new Dictionary<ClientId, ConnectionController>();
        }

        /// <summary> Thread-safe. Add connection to the list of incoming connections. Wake 'Loop'.</summary>
        public void AddConnection(ConnectionController connection)
        {
            _queue.Enqueue(connection);
            Wake();
        }

        /// <summary>
        /// Signals the thread blocked on 'Loop' to wake up.
        /// </summary>
        public void Wake()
        {
            _mre.Set();
        }

        /// <summary>
        /// All the controllers are passing their work back to the dispatchers
        /// which - in turn - route those messages to their proper destination.
        /// </summary>
        public void Loop(CancellationToken cancel)
        {
            if (OnConnectionAccepted == null)
                throw new InvalidOperationException("OnConnectionAccepted has not been initialized.");

            if (OnBlockMessageDispatched == null)
                throw new InvalidOperationException("OnBlockMessageDispatched has not been initialized.");

            for (var i = 0; i < OnCoinMessageDispatched.Length; i++)
                if (OnCoinMessageDispatched[i] == null)
                    throw new InvalidOperationException("OnCoinMessageDispatched has not been initialized.");

            _log?.Log(LogSeverity.Info, "DispatchController started.");

            while (!cancel.IsCancellationRequested)
            {
                _mre.WaitOne();
                _mre.Reset();

                bool workDone;
                do
                {
                    // Process incoming connections
                    workDone = HandleNewConnection();

                    // Process pending inbox messages
                    workDone |= HandleRequest();
                } while (workDone);
            }

            foreach (var (_, conn) in _connections)
            {
                conn.Stop();
                conn.Join();
            }

            _log?.Log(LogSeverity.Info, "DispatchController exited.");
        }

        public bool HandleNewConnection()
        {
            var workDone = false;

            if (_queue.TryDequeue(out var connection))
            {
                workDone = true;

                if (_connections.Count >= Constants.MaxActiveConnections)
                {
                    // Signal that this connection will not be taken into account.
                    Span<byte> buffer = stackalloc byte[ProtocolErrorResponse.SizeInBytes];
                    var errorMessage = new ProtocolErrorResponse(
                        buffer, RequestId.MinRequestId, ClientId.MinClientId, ProtocolErrorStatus.TooManyActiveClients);

                    connection.Send(errorMessage.Span);
                }
                else
                {
                    connection.OnRequestReceived = Wake;
                    _connections.Add(connection.ClientId, connection);

                    // Intended to start the threads associated to the connection.
                    OnConnectionAccepted(connection);
                }
            }

            return workDone;
        }

        public bool HandleRequest()
        {
            if (!_inbox.CanPeek)
            {
                return false;
            }

            var next = _inbox.Peek().Span;

            try
            {
                var message = new Message(next);
                var mask = message.Header.ClientId.Mask;
                var kind = message.Header.MessageKind;
                var requestId = message.Header.RequestId;
                var clientId = message.Header.ClientId;

                if (!_connections.TryGetValue(clientId, out var connection))
                {
                    // Outdated message, drop and move on.
                    return true;
                }

                if (!connection.Connected || kind == MessageKind.CloseConnection)
                {
                    // Client disconnected, drop message and remove connection.
                    _connections.Remove(clientId);
                    return true;
                }

                if (kind.IsResponse())
                {
                    // Forward responses to their respective 'ConnectionController'.
                    connection.Send(next);
                    return true;
                }

                if (kind.IsForCoinController())
                {
                    // Multiple coin controllers
                    Outpoint outpoint;
                    switch (kind)
                    {
                        case MessageKind.GetCoin:
                            outpoint = new GetCoinRequest(next, mask).Outpoint;
                            break;

                        case MessageKind.ProduceCoin:
                            outpoint = new ProduceCoinRequest(next, mask).Outpoint;
                            break;

                        case MessageKind.ConsumeCoin:
                            outpoint = new ConsumeCoinRequest(next, mask).Outpoint;
                            break;

                        case MessageKind.RemoveCoin:
                            outpoint = new RemoveCoinRequest(next, mask).Outpoint;
                            break;

                        default:
                            throw new NotSupportedException();
                    }

                    // Sharding based on the outpoint hash

                    // Beware: the factor 'BigPrime' is used to avoid accidental factor collision
                    // between the sharding performed at the dispatch controller level, and the
                    // sharding performed within the Sozu table.

                    // PERF: hashing the outpoint is repeated in the CoinController itself
                    const ulong BigPrime = 1_000_000_007;
                    var controllerIndex = (int) ((_hash.Hash(ref outpoint) % BigPrime) 
                                                 % (ulong) _coinControllerBoxes.Length);

                    var written = _coinControllerBoxes[controllerIndex].TryWrite(next);
                    if (written) OnCoinMessageDispatched[controllerIndex]();
                    else
                    {
                        // Coin controller is saturated.
                        Span<byte> buffer = stackalloc byte[ProtocolErrorResponse.SizeInBytes];
                        var errorMessage = new ProtocolErrorResponse(
                            buffer, requestId, clientId, ProtocolErrorStatus.ServerBusy);

                        connection.Send(errorMessage.Span);
                    }

                    return true;
                }

                {
                    // Block controller
                    var written = _chainControllerBox.TryWrite(next);
                    if (written) OnBlockMessageDispatched();
                    else
                    {
                        // Block controller is saturated.
                        Span<byte> buffer = stackalloc byte[ProtocolErrorResponse.SizeInBytes];
                        var errorMessage = new ProtocolErrorResponse(
                            buffer, requestId, clientId, ProtocolErrorStatus.ServerBusy);

                        connection.Send(errorMessage.Span);
                    }
                }
            }
            finally
            {
                _inbox.Next();
            }

            return true;
        }
    }
}