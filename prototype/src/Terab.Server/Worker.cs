using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Terab.UTXO.Core;
using Terab.UTXO.Core.Messaging;
using Terab.UTXO.Core.Sozu;

namespace Terab.Server
{
    /// <summary>
    /// Wrap a shard of Sozu table, make it run in dedicated worker task.
    /// Hold also shared <see cref="BoundedInbox"/> for data exchange with
    /// <see cref="UTXO.Core.Networking.Dispatcher"/>.
    /// </summary>
    public class Worker
    {
        // TODO: [vermorel] pass fields as readonly whenever possible

        /// <summary>
        /// Long running task managing a shard of Sozu table.
        /// </summary>
        private Task _task;

        /// <summary>
        /// Signal cancellation of task.
        /// </summary>
        private CancellationTokenSource _source;

        /// <summary>
        /// Inbox where incoming client requests are routed from.
        /// </summary>
        private BoundedInbox _inbox;

        /// <summary>
        /// Outbox where read / write results are routed to.
        /// </summary>
        private BoundedInbox _outbox;

        /// <summary>
        /// Abstract sozu table.
        /// </summary>
        private ITxoTable _txoTable;

        // TODO: [vermorel] the 'CancellationTokenSource' should always be the last argument (convention).

        public Worker(BoundedInbox inbox, BoundedInbox outbox, CancellationTokenSource source, ITxoTable txoTable)
        {
            _inbox = inbox;
            _outbox = outbox;
            _source = source;
            _txoTable = txoTable;
            _task = new Task(Work, _source.Token, TaskCreationOptions.LongRunning);
        }

        public void Start()
        {
            _task.Start();
        }

        public void Stop()
        {
            _source.Cancel();
        }

        private void Work()
        {
            var txoIn = TxoPack.Create();
            var txoOut = TxoPack.Create();

            // local array for capturing outpoints from inbox then sorting
            var outpoints = new Outpoint[Constants.MaxOutpointsCount];

            // array of inbox messages headers
            var messageHeaders = new byte[Constants.MaxOutpointsCount][];

            // array of inbox messages payloads
            // var payloads = new byte[MaxTxoCount][]; // not used in read request

            // provide index array for argsort 
            var argsort = new int[Constants.MaxOutpointsCount];

            int txoCount = 0;

            while (!_source.Token.IsCancellationRequested)
            {
                var message = _inbox.Peek();
                if (message.Length > 0)
                {
                    // parse current message and add txo to txopack
                    var outpoint =
                        Outpoint.CreateNaked(
                            message.Slice(ClientServerMessage.PayloadStart, Outpoint.KeySizeInBytes));
                    outpoints[txoCount] = outpoint;
                    messageHeaders[txoCount] = message.Slice(0, ClientServerMessage.PayloadStart).ToArray();
                    // payloads[messageCount] = message.Slice(ClientServerMessage.PayloadStart).ToArray();
                    // not used in read request
                    argsort[txoCount] = txoCount;
                    ++txoCount;

                    Debug.Assert(txoCount <= Constants.MaxOutpointsCount,
                        "MaxTxoCount reached when looping inbox messages.");

                    _inbox.Next(message.Length);
                }
                else
                {
                    // current round of inbox message picking is over
                    Array.Sort(outpoints, argsort, 0, txoCount);
                    // sort txopack
                    ref var writer = ref txoIn.GetResetWriter();

                    // identify identical outpoint, and write querying txopack
                    ref var previous = ref outpoints[argsort[0]];
                    writer.Write(in previous, ReadOnlySpan<byte>.Empty);
                    for (ushort index = 1; index < txoCount; ++index)
                    {
                        ref var current = ref outpoints[argsort[index]];
                        if (previous != current)
                        {
                            previous = current;
                            writer.Write(in previous, ReadOnlySpan<byte>.Empty);
                        }
                    }

                    // querying
                    _txoTable.Read(txoIn, txoOut); // or write depends on the message

                    // fill outbox
                    var iter = txoOut.GetForwardIterator();
                    for (int index = 0; index < txoCount; ++index)
                    {
                        // TODO: [vermorel] clarify the purpose of what is being done here

                        ref var op = ref outpoints[argsort[index]];

                        while (iter.EndNotReached)
                        {
                            if (iter.Current < op) // TODO: [vermorel] clarify those conditions
                            {
                                iter.MoveNext();
                            }
                            else if (iter.Current > op)
                            {
                                Span<byte> reply = MakeOutpointNotFoundReply(ref op);
                                _outbox.TryWrite(reply);
                                break;
                            }
                            else
                            {
                                // request outpoint found, make a reply, process next message
                                // but keep the txopack iterator unmoved
                                Span<byte> reply = MakeReply(ref iter, messageHeaders[argsort[index]]);
                                _outbox.TryWrite(reply);
                                break;
                            }
                        }

                        if (!iter.EndNotReached)
                        {
                            // in this case all remaining messages don't have matching outpoint
                            Span<byte> reply = MakeOutpointNotFoundReply(ref op);
                            _outbox.TryWrite(reply);
                        }
                    }

                    // reset local variables
                    txoCount = 0; // TODO: [vermorel] move the declaration of this counter inside the lopp
                }
            }
        }

        private Span<byte> MakeOutpointNotFoundReply(ref Outpoint outpoint)
        {
            throw new NotImplementedException();
        }

        private Span<byte> MakeReply(ref TxoForwardIterator iter, byte[] messageHeader)
        {
            throw new NotImplementedException();
        }
    }
}