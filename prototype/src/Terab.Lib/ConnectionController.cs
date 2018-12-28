// Copyright Lokad 2018 under MIT BCH.
using System;
using System.Net.Sockets;
using System.Threading;
using Terab.Lib.Messaging;
using Terab.Lib.Messaging.Protocol;
using Terab.Lib.Networking;

namespace Terab.Lib
{
    /// <summary>
    /// Manage the connection to one Terab client, and forward its
    /// requests to the dispatch controller through a BoundedInbox.
    /// </summary>
    public class ConnectionController
    {
        private const int ResponsePoolSize = 65536;

        /// <summary>
        /// Intended to keep responses fitting in a single typically sized
        /// transport unit (aka maxing-out the MTU) at 1400 ~ 1500 bytes.
        /// </summary>
        private const int ResponseBatchSize = 12;

        private readonly BoundedInbox _dispatchInbox;

        private readonly BoundedInbox _outbox;

        private readonly ISocketLike _socket;

        /// <summary>
        /// ID of the client connected to the socket.
        /// </summary>
        private readonly ClientId _clientId;

        private readonly byte[] _bufferIn;

        private readonly ILog _log;

        public Action OnRequestReceived { get; set; }

        public ClientId ClientId => _clientId;

        private readonly ManualResetEvent _mre;

        private readonly CancellationTokenSource _tokenSource;

        public bool Connected => _socket.Connected;

        private Thread _threadIn;

        private Thread _threadOut;

        /// <summary> Counts request in-progress associated to this connection. </summary>
        private volatile int _requestsInProgress;

        /// <summary> Intended to avoid TCP packet fragmentation. </summary>
        private readonly SpanPool<byte> _responsePool;

        /// <summary> Counts the number of responses buffered in '_responsePool'. </summary>
        private int _responseCountInPool;

        public ConnectionController(BoundedInbox dispatchInbox, ISocketLike socket, ClientId clientId, ILog log = null)
        {
            _dispatchInbox = dispatchInbox ?? throw new ArgumentNullException(nameof(dispatchInbox));
            _outbox = new BoundedInbox(Constants.ConnectionControllerOutboxSize);
            _socket = socket ?? throw new ArgumentNullException(nameof(socket));
            _clientId = clientId;
            _log = log;
            _bufferIn = new byte[Constants.MaxRequestSize];
            _mre = new ManualResetEvent(false);
            _tokenSource = new CancellationTokenSource();
            _requestsInProgress = 0;
            _responsePool = new SpanPool<byte>(ResponsePoolSize);
            _responseCountInPool = 0;
        }

        public void Start()
        {
            // Process incoming requests
            _threadIn = new Thread(LoopIn)
            {
                Name = $"ConnectionIn({ClientId})",
                IsBackground = true
            };
            _threadIn.Start();
            _log?.Log(LogSeverity.Debug, $"Thread started: {_threadIn.Name}.");

            // Process outgoing responses
            _threadOut = new Thread(LoopOut)
            {
                Name = $"ConnectionOut({ClientId})",
                IsBackground = true
            };
            _threadOut.Start();
            _log?.Log(LogSeverity.Info, $"Thread started: {_threadOut.Name}.");
        }

        public void Stop()
        {
            _socket.Close();
            _tokenSource.Cancel();
            _mre.Set();
        }

        public void Join()
        {
            _threadIn?.Join();
            _threadOut?.Join();
        }

        public void LoopIn()
        {
            if(OnRequestReceived == null)
                throw new InvalidOperationException("OnRequestReceived is null.");

            try
            {
                while (!_tokenSource.Token.IsCancellationRequested && _socket.Connected)
                {
                    // Blocking call.
                    if (HandleRequest())
                        // Notify the dispatch controller.
                        OnRequestReceived();
                }
            }
            catch (SocketException ex)
            {
                _log?.Log(LogSeverity.Info, $"ConnectionController({ClientId}) closed in LoopIn(): {ex}.");
            }
            catch (ObjectDisposedException ex)
            {
                _log?.Log(LogSeverity.Info, $"ConnectionController({ClientId}) closed in LoopIn(): {ex}.");
            }
            finally
            {
                Stop();
               
                // Notify the dispatcher that the connection has been closed.
                var buffer = new byte[CloseConnectionResponse.SizeInBytes]; // allocation required because of 'finally'
                if (!_dispatchInbox.TryWrite(
                    new CloseConnectionResponse(buffer, RequestId.MinRequestId, _clientId).Span))
                {
                    _log?.Log(LogSeverity.Error, $"ConnectionController({ClientId}) can't notify dispatch controller of its own termination.");
                }
            }
        }

        /// <summary>
        /// Thread-safe. Signals the thread blocked on 'LoopOut' to wake up.
        /// </summary>
        public void Send(Span<byte> response)
        {
            if (!_outbox.TryWrite(response))
            {
                if (!_tokenSource.IsCancellationRequested)
                {
                    _log?.Log(LogSeverity.Warning, $"Connection({ClientId}): outbox full.");
                }

                // If outbox is full, terminate the connection.
               Stop();
            }

            _mre.Set();
        }

        public void LoopOut()
        {
            try
            {
                while (!_tokenSource.Token.IsCancellationRequested)
                {
                    _mre.WaitOne();
                    _mre.Reset();

                    while (!_tokenSource.Token.IsCancellationRequested && _socket.Connected && 
                           HandleResponse())
                    { }

                }
            }
            catch (SocketException ex)
            {
                _log?.Log(LogSeverity.Info, $"ConnectionController({ClientId}) closed in LoopOut(): {ex}.");
            }
            catch (ObjectDisposedException ex)
            {
                _log?.Log(LogSeverity.Info, $"ConnectionController({ClientId}) closed in LoopOut(): {ex}.");
            }
            finally
            {
                Stop();
            }
        }

        public bool HandleRequest()
        {
            // Blocking until header is received.
            _socket.Receive(new Span<byte>(_bufferIn, 0, MessageHeader.SizeInBytes));

            var message = new Message(_bufferIn);

            // Request too short
            if (message.SizeInBytes <= MessageHeader.SizeInBytes)
            {
                Span<byte> errorBuffer = stackalloc byte[ProtocolErrorResponse.SizeInBytes];
                var errorMessage = new ProtocolErrorResponse(errorBuffer,
                    RequestId.MinRequestId, ClientId.MinClientId, ProtocolErrorStatus.RequestTooShort);

                Send(errorMessage.Span);
                return false;
            }

            var kind = message.Header.MessageKind;
            var requestId = message.Header.RequestId;
            

            // Request too long
            if (message.SizeInBytes >= Constants.MaxRequestSize)
            {
                Span<byte> errorBuffer = stackalloc byte[ProtocolErrorResponse.SizeInBytes];
                var errorMessage = new ProtocolErrorResponse(errorBuffer,
                    requestId, ClientId.MinClientId, ProtocolErrorStatus.RequestTooLong);

                Send(errorMessage.Span);
                return false;
            }

            // Invalid message kind
            if (!kind.IsDefined() || kind.IsResponse())
            {
                // Unknown kind is invalid.
                // Response kind is invalid.
                Span<byte> buffer = stackalloc byte[ProtocolErrorResponse.SizeInBytes];
                var errorMessage = new ProtocolErrorResponse(
                    buffer, requestId, ClientId.MinClientId, ProtocolErrorStatus.InvalidMessageKind);

                Send(errorMessage.Span);
                return false;
            }

            // Blocking until the rest of the message is received.
            _socket.Receive(new Span<byte>(_bufferIn, 
                MessageHeader.SizeInBytes,
                message.SizeInBytes - MessageHeader.SizeInBytes));

            message.Header.ClientId = _clientId;

            // Client request to close the connection.
            if (message.Header.MessageKind == MessageKind.CloseConnection)
            {
                Span<byte> buffer = stackalloc byte[CloseConnectionResponse.SizeInBytes];
                var closeResponse = new CloseConnectionResponse(buffer, requestId, ClientId.MinClientId);

                _outbox.TryWrite(closeResponse.Span);
            }

            // Forwards the message to the dispatch controller.
            if (_dispatchInbox.TryWrite(message.Span))
            {
                Interlocked.Increment(ref _requestsInProgress);
                return true;
            }

            //  The dispatch inbox is full.
            {
                Span<byte> errorBuffer = stackalloc byte[ProtocolErrorResponse.SizeInBytes];
                var errorMessage = new ProtocolErrorResponse(errorBuffer,
                    requestId, ClientId.MinClientId, ProtocolErrorStatus.ServerBusy);

                Send(errorMessage.Span);
            }

            return false;
        }

        public bool HandleResponse()
        {
            if (!_outbox.CanPeek)
            {
                return false;
            }

            var next = _outbox.Peek().Span;

            try
            {
                var message = new Message(next);
                var kind = message.Header.MessageKind;

                // Remove client ID from message
                message.Header.ClientId = default;

                if (_requestsInProgress >= ResponseBatchSize || _responseCountInPool > 0)
                {
                    var nextResponse = _responsePool.GetSpan(next.Length);
                    next.CopyTo(nextResponse);
                    _responseCountInPool++;

                    if (_responseCountInPool >= ResponseBatchSize)
                    {
                        _socket.Send(_responsePool.Allocated());
                        _responsePool.Reset();
                        _responseCountInPool = 0;
                    }
                }
                else
                {
                    _socket.Send(next);
                }
                
                Interlocked.Decrement(ref _requestsInProgress);

                // Some responses trigger the termination of the controller.
                if (kind == MessageKind.CloseConnectionResponse
                    || kind == MessageKind.ProtocolErrorResponse)
                {
                    if (kind == MessageKind.ProtocolErrorResponse)
                    {
                        var protocolResponse = new ProtocolErrorResponse(next);
                        _log?.Log(LogSeverity.Info, $"ConnectionController({ClientId}) on protocol error {protocolResponse.Status}.");
                    }

                    if (_responseCountInPool > 0)
                    {
                        _socket.Send(_responsePool.Allocated());
                        _responsePool.Reset();
                        _responseCountInPool = 0;
                    }

                    _tokenSource.Cancel();
                }
            }
            finally
            {
                _outbox.Next();
            }

            return true;
        }
    }
}
