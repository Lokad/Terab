using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Terab.UTXO.Core.Messaging;

namespace Terab.UTXO.Core.Networking
{
    /// <summary>
    /// Is the central communication manager and coordinator between the
    /// messages received by the Terab clients and the threads that are
    /// available to treat them. It is mono-threaded and the bottleneck through
    /// which any message will have to pass if it is to be treated by the Terab
    /// application. The responses returned by the threads also pass through
    /// here and are redirected by the dispatcher to the corresponding
    /// <see cref="ClientConnection"/> to be sent back to the demanding party.
    /// </summary>
    public class Dispatcher
    {
        private readonly ConcurrentQueue<ClientConnection> _queue;

        // TODO: [vermorel] The intent of the '_maxClientNumber' is very unclear.
        // TODO: [vermorel] Should be renamed 'MaxActiveConnections' (and moved to 'Configuration').
        private readonly uint _maxClientNumber;

        private readonly Dictionary<ClientId, ClientConnection> _activeConnections =
            new Dictionary<ClientId, ClientConnection>();

        // TODO: [vermorel] Inconsistent names '_outbox' vs '_controllerInbox'. Should be aligned.
        private readonly BoundedInbox _outbox;
        
        private readonly BoundedInbox[] _workerInboxes;

        private readonly BoundedInbox _controllerInbox;

        // TODO: [vermorel] The size of a buffer should not be named 'PayloadStart'
        private readonly byte[] _errorBuffer = new byte[ClientServerMessage.PayloadStart];

        public Dispatcher(
            ConcurrentQueue<ClientConnection> queue, 
            uint maxClientNumber, 
            BoundedInbox outbox, 
            BoundedInbox[] workerInboxes, 
            BoundedInbox controllerInbox)
        {
            // TODO: [vermorel] Isolate power-of-2 check as a 'Math' extension and unit-test accordingly.

            // verify that the number of inboxes is a power of 2
            if(workerInboxes.Length == 0 || Math.Log(workerInboxes.Length , 2) % 1 != 0)
                throw new ArgumentException(
                    $"The number of sharded inboxes is {workerInboxes.Length}. Should be a power of 2.");

            _queue = queue;
            _maxClientNumber = maxClientNumber;

            _outbox = outbox;
            _workerInboxes = workerInboxes;
            _controllerInbox = controllerInbox;
        }

        public void DequeueNewClients()
        {
            while (_queue.TryDequeue(out var newConnection))
            {
                if (_activeConnections.Count >= _maxClientNumber)
                {
                    // TODO: [vermorel] Error message is incorrect. Should be 'MaxActiveConnectionExceeded'.

                    // Signal that this connection will not be taken into account
                    newConnection.TryWrite(MessageCreationHelper.NoMoreSpaceForClientsMessage);
                    newConnection.Close();
                }
                else
                {
                    _activeConnections.Add(newConnection.ConnectionId, newConnection);
                }
            }
        }

        // TODO: [vermorel] Method named 'listen' but actually 'remove'?. Intent to be clarified.
        public void ListenToConnections()
        {
            // TODO: [vermorel] Directly initialize, it will remove two test conditions below, perf overhead should be insignificant.
            List<ClientId> toRemove = null;

            foreach (var kv in _activeConnections)
            {
                if (!kv.Value.IsConnected)
                {
                    if (toRemove == null)
                    {
                        toRemove = new List<ClientId>();
                    }
                    toRemove.Add(kv.Key);
                    continue;
                }
                
                kv.Value.Send();

                // TODO: [vermorel] Almost but not quite like an iterator. Design should be aligned with other iterators.
                var nextMessage = kv.Value.ReceiveNext();
                while (nextMessage.Length != 0)
                {
                    var toWriteInto = ClientServerMessage.IsSharded(nextMessage)
                        ? _workerInboxes[
                            // TODO: [vermorel] Do not keep the shardling logic inline, isolate with a helper.
                            ClientServerMessage.FirstKeyByte(nextMessage) % _workerInboxes.Length]
                        : _controllerInbox;

                    if (!toWriteInto.TryWrite(nextMessage))
                    {
                        // TODO: [vermorel] Oddly written statement, to be rewritten.
                        new Span<byte>(_errorBuffer).EmitWorkersBusy(nextMessage);
                        kv.Value.TryWrite(_errorBuffer);
                    }

                    nextMessage = kv.Value.ReceiveNext();
                }
            }

            // Delete connections that were found unconnected
            if(toRemove != null)
                foreach (var clientId in toRemove)
                {
                    _activeConnections.Remove(clientId);
                }
        }

        public void SendResponses()
        {
            var toSend = _outbox.Peek();
            while (toSend.Length != 0)
            {
                if(_activeConnections.TryGetValue(ClientServerMessage.GetClientId(toSend), out var client))
                    client.TryWrite(toSend);

                _outbox.Next();
                toSend = _outbox.Peek();
            }
        }

        // TODO: [vermorel] To be renamed as 'DoWork' and intent should be clarified it's a no-return method.
        public void Dispatch()
        {
            while (true)
            {
                // TODO: [vermorel] Don't design methods that can spin-loop at CPU clock speed.

                // First, see whether there are new connections to take into account
                DequeueNewClients();

                // Handle existing connections
                ListenToConnections();

                // Send out answers
                SendResponses();
            }
        }
    }
}
