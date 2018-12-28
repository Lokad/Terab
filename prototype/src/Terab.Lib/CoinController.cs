// Copyright Lokad 2018 under MIT BCH.
using System;
using System.Threading;
using Terab.Lib.Chains;
using Terab.Lib.Coins;
using Terab.Lib.Messaging;
using Terab.Lib.Messaging.Protocol;

namespace Terab.Lib
{
    /// <summary>
    /// Processes a shard of read/write operations associated to coins.
    /// </summary>
    public class CoinController
    {
        private const int PoolSizeInBytes = 65536;

        /// <summary>
        /// Inbox where incoming client requests are routed from.
        /// </summary>
        private readonly BoundedInbox _inbox;

        private readonly BoundedInbox _outbox;

        private readonly ICoinStore _store;

        private readonly IOutpointHash _hash;

        private readonly ILog _log;

        private readonly int _shardIndex;

        private ILineage _lineage;

        private readonly SpanPool<byte> _pool;

        private readonly ManualResetEvent _mre;

        /// <summary>
        /// Called by the thread blocking on 'Look' whenever a request is handled.
        /// </summary>
        public Action OnRequestHandled { get; set; }

        public CoinController(BoundedInbox inbox, BoundedInbox outbox, ICoinStore store, IOutpointHash hash,
            ILog log = null, int shardIndex = -1)
        {
            _inbox = inbox;
            _outbox = outbox;
            _store = store;
            _hash = hash;
            _log = log;
            _shardIndex = shardIndex;

            _pool = new SpanPool<byte>(PoolSizeInBytes);
            _mre = new ManualResetEvent(false);
        }

        /// <remarks>
        /// Volatile operation provides not only atomicity but also a memory
        /// barrier which guarantees that later ILineage capturing in
        /// <see cref="HandleRequest"/> would happen really on the new instance.
        /// </remarks>
        public ILineage Lineage
        {
            get => Volatile.Read(ref _lineage);
            set => Volatile.Write(ref _lineage, value);
        }

        /// <summary>
        /// Signals the thread blocked on 'Loop' to wake up.
        /// </summary>
        public void Wake()
        {
            _mre.Set();
        }

        /// <summary>
        /// Blocking call, only exits when cancellation is requested.
        /// </summary>
        public void Loop(CancellationToken cancel)
        {
            if (OnRequestHandled == null)
                throw new InvalidOperationException("OnRequestHandled has not been initialized.");

            _log?.Log(LogSeverity.Info, $"CoinController({_shardIndex}) started.");

            while (!cancel.IsCancellationRequested)
            {
                _mre.WaitOne();
                _mre.Reset();

                var requestHandled = false;
                while (HandleRequest())
                    requestHandled = true;

                if (requestHandled) OnRequestHandled();
            }

            _log?.Log(LogSeverity.Info, $"CoinController({_shardIndex}) exited.");
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
                var kind = message.Header.MessageKind;
                var mask = message.Header.ClientId.Mask;
                var requestId = message.Header.RequestId;
                var clientId = message.Header.ClientId;

                Span<byte> response;
                switch (kind)
                {
                    case MessageKind.GetCoin:
                    {
                        var request = new GetCoinRequest(next, mask);
                        var hash = _hash.Hash(ref request.Outpoint);

                        var found = _store.TryGet(hash, ref request.Outpoint, request.Context, _lineage,
                            out Coin coin,
                            out var production, out var consumption);

                        if (found)
                        {
                            var foundResponse = new GetCoinResponse(
                                requestId,
                                clientId,
                                GetCoinStatus.Success,
                                ref request.Outpoint,
                                coin.Flags.ToClientFlags(),
                                request.Context,
                                production,
                                consumption,
                                coin.Payload.Satoshis,
                                coin.Payload.NLockTime,
                                coin.Payload.Script,
                                mask,
                                _pool);

                            response = foundResponse.Span;
                        }
                        else
                        {
                            var notFoundResponse = new GetCoinResponse(
                                requestId,
                                clientId,
                                GetCoinStatus.OutpointNotFound,
                                ref request.Outpoint,
                                OutpointFlags.None,
                                request.Context,
                                production: BlockAlias.Undefined,
                                consumption: BlockAlias.Undefined,
                                satoshis: 0,
                                nLockTime: 0,
                                script: Span<byte>.Empty,
                                mask,
                                _pool);

                            response = notFoundResponse.Span;
                        }

                        break;
                    }

                    case MessageKind.ProduceCoin:
                    {
                        var request = new ProduceCoinRequest(next, mask);
                        var hash = _hash.Hash(ref request.Outpoint);
                        var context = request.Context;

                        CoinChangeStatus status;

                        if (_lineage.IsUncommitted(context))
                        {
                            status = _store.AddProduction(
                                hash,
                                ref request.Outpoint,
                                request.Flags == OutpointFlags.IsCoinbase,
                                new Payload(request.Satoshis, request.NLockTime, request.Script, _pool),
                                context,
                                _lineage);
                        }
                        else
                        {
                            status = CoinChangeStatus.InvalidContext;
                        }

                        switch (status)
                        {
                            case CoinChangeStatus.Success:
                                response = new ProduceCoinResponse(
                                    requestId, clientId, ChangeCoinStatus.Success, _pool).Span;
                                break;

                            case CoinChangeStatus.InvalidContext:
                                response = new ProduceCoinResponse(
                                    requestId, clientId, ChangeCoinStatus.InvalidContext, _pool).Span;
                                break;

                            case CoinChangeStatus.InvalidBlockHandle:
                                response = new ProduceCoinResponse(
                                    requestId, clientId, ChangeCoinStatus.InvalidBlockHandle, _pool).Span;
                                break;

                            default:
                                throw new NotSupportedException();
                        }

                        break;
                    } // end of 'MessageKind.ProduceCoin'

                    case MessageKind.ConsumeCoin:
                    {
                        var request = new ConsumeCoinRequest(next, mask);
                        var hash = _hash.Hash(ref request.Outpoint);
                        var context = request.Context;

                        CoinChangeStatus status;

                        if (_lineage.IsUncommitted(context))
                        {
                            status = _store.AddConsumption(
                                hash,
                                ref request.Outpoint,
                                context,
                                _lineage);
                        }
                        else
                        {
                            status = CoinChangeStatus.InvalidContext;
                        }

                        switch (status)
                        {
                            case CoinChangeStatus.Success:
                                response = new ConsumeCoinResponse(
                                    requestId, clientId, ChangeCoinStatus.Success, _pool).Span;
                                break;

                            case CoinChangeStatus.InvalidContext:
                                response = new ConsumeCoinResponse(
                                    requestId, clientId, ChangeCoinStatus.InvalidContext, _pool).Span;
                                break;

                            case CoinChangeStatus.InvalidBlockHandle:
                                response = new ConsumeCoinResponse(
                                    requestId, clientId, ChangeCoinStatus.InvalidBlockHandle, _pool).Span;
                                break;

                            case CoinChangeStatus.OutpointNotFound:
                                response = new ConsumeCoinResponse(
                                    requestId, clientId, ChangeCoinStatus.OutpointNotFound, _pool).Span;
                                break;

                            default:
                                throw new NotSupportedException();
                        }

                        break;
                    } // end of 'MessageKind.ConsumeCoin' 

                    case MessageKind.RemoveCoin:
                    {
                        var request = new RemoveCoinRequest(next, mask);
                        var hash = _hash.Hash(ref request.Outpoint);
                        var context = request.Context;

                        CoinChangeStatus status;

                        if (_lineage.IsUncommitted(context))
                        {
                            var option = CoinRemoveOption.None;
                            if (request.RemoveProduction) option |= CoinRemoveOption.RemoveProduction;
                            if (request.RemoveConsumption) option |= CoinRemoveOption.RemoveConsumption;

                            status = _store.Remove(
                                hash,
                                ref request.Outpoint,
                                context,
                                option,
                                _lineage);
                        }
                        else
                        {
                            status = CoinChangeStatus.InvalidContext;
                        }

                        switch (status)
                        {
                            case CoinChangeStatus.Success:
                                response = new RemoveCoinResponse(
                                    requestId, clientId, ChangeCoinStatus.Success, _pool).Span;
                                break;

                            case CoinChangeStatus.InvalidContext:
                                response = new RemoveCoinResponse(
                                    requestId, clientId, ChangeCoinStatus.InvalidContext, _pool).Span;
                                break;

                            case CoinChangeStatus.InvalidBlockHandle:
                                response = new RemoveCoinResponse(
                                    requestId, clientId, ChangeCoinStatus.InvalidBlockHandle, _pool).Span;
                                break;

                            case CoinChangeStatus.OutpointNotFound:
                                response = new RemoveCoinResponse(
                                    requestId, clientId, ChangeCoinStatus.OutpointNotFound, _pool).Span;
                                break;

                            default:
                                throw new NotSupportedException();
                        }

                        break;
                    } // end of 'MessageKind.RemoveCoin' 

                    default:
                        throw new NotSupportedException();
                }


                while (!_outbox.TryWrite(response))
                {
                    _log?.Log(LogSeverity.Warning, $"CoinController can't write the response to {kind}.");
                    // Pathological situation, we don't want to overflow the logger.
                    Thread.Sleep(1000);
                }
            }
            finally
            {
                _pool.Reset();
                _inbox.Next();
            }

            return true;
        }
    }
}