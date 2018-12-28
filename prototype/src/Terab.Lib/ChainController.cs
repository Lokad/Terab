// Copyright Lokad 2018 under MIT BCH.
using System;
using System.Threading;
using Terab.Lib.Chains;
using Terab.Lib.Messaging;
using Terab.Lib.Messaging.Protocol;
using CommitBlockStatus = Terab.Lib.Chains.CommitBlockStatus;
using OpenBlockStatus = Terab.Lib.Chains.OpenBlockStatus;

namespace Terab.Lib
{
    /// <summary>
    /// Manages the state of the blockchain (actually a tree of blocks).
    /// </summary>
    public class ChainController
    {
        /// <summary>
        /// Intended to ensure the persistence of the blockchain.
        /// </summary>
        private readonly IChainStore _store;

        /// <summary>
        /// The optimized blockchain that the controller is going to base
        /// its responses on.
        /// </summary>
        private ILineage _lineage;

        /// <summary>
        /// Intended to propagate the new lineage to all shards.
        /// </summary>
        private readonly Action<ILineage> _propagateLineage;

        private readonly ILog _log;

        /// <summary>
        /// Inbox where the <see cref="DispatchController"/> will write client requests
        /// into.
        /// </summary>
        private readonly BoundedInbox _inbox;

        /// <summary>
        /// Outbox where the controller will write the responses
        /// to the requests into, which will be sent to the corresponding client
        /// by the <see cref="DispatchController"/>.
        /// </summary>
        private readonly BoundedInbox _outbox;

        private readonly SpanPool<byte> _pool;

        private readonly ManualResetEvent _mre;

        /// <summary>
        /// Called by the thread blocking on 'Look' whenever a request is handled.
        /// </summary>
        public Action OnRequestHandled { get; set; }

        public ChainController(IChainStore store, BoundedInbox inbox, BoundedInbox outbox,
            Action<ILineage> propagateLineage, ILog log = null)
        {
            _store = store;
            _inbox = inbox;
            _outbox = outbox;
            _propagateLineage = propagateLineage;
            _log = log;

            _pool = new SpanPool<byte>(GetBlockInfoResponse.SizeInBytes);
            _mre = new ManualResetEvent(false);

            RefreshAndPropagateLineage();
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

            _log?.Log(LogSeverity.Info, "ChainController started.");

            while (!cancel.IsCancellationRequested)
            {
                _mre.WaitOne();
                _mre.Reset();

                var requestHandled = false;
                while (HandleRequest())
                    requestHandled = true;

                if (requestHandled) OnRequestHandled();
            }

            _log?.Log(LogSeverity.Info, "ChainController exited.");
        }

        private void RefreshAndPropagateLineage()
        {
            _lineage = _store.GetLineage();
            _propagateLineage(_lineage);
        }


        /// <summary>
        /// Return 'true' if at least one request has been processed, 'false' otherwise.
        /// </summary>
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

                Span<byte> response;
                switch (kind)
                {
                    case MessageKind.OpenBlock:
                        response = OpenBlock(new OpenBlockRequest(message.Span, mask)).Span;
                        break;

                    case MessageKind.GetBlockHandle:
                        response = GetBlockHandle(new GetBlockHandleRequest(message.Span, mask)).Span;
                        break;

                    case MessageKind.CommitBlock:
                        response = CommitBlock(new CommitBlockRequest(message.Span, mask)).Span;
                        break;

                    case MessageKind.GetBlockInfo:
                        response = GetBlockInfo(new GetBlockInfoRequest(message.Span, mask));
                        break;
                    default:
                        throw new NotSupportedException();
                }

                while (!_outbox.TryWrite(response))
                {
                    _log?.Log(LogSeverity.Warning, $"ChainController can't write the response to {kind}.");
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

        public OpenBlockResponse OpenBlock(OpenBlockRequest request)
        {
            // The request associated to the genesis block does not come with a valid parent.
            var status = _store.TryOpenBlock( request.ParentId, out var block);

            switch (status)
            {
                case OpenBlockStatus.Success:
                    RefreshAndPropagateLineage();
                    return new OpenBlockResponse(ref request.MessageHeader, request.Mask, block.Alias, block.BlockId,
                        _pool);

                case OpenBlockStatus.ParentNotFound:
                    return new OpenBlockResponse(ref request.MessageHeader, Messaging.OpenBlockStatus.ParentNotFound,
                        _pool);

                default:
                    throw new NotSupportedException();
            }
        }

        public GetBlockHandleResponse GetBlockHandle(GetBlockHandleRequest request)
        {
            BlockAlias alias;
            var status = request.IsCommitted
                ? _store.TryGetAlias(request.CommittedBlockId, out alias)
                : _store.TryGetAlias(request.UncommittedBlockId, out alias);

            switch (status)
            {
                case GetBlockAliasStatus.Success:
                    return new GetBlockHandleResponse(ref request.MessageHeader, request.Mask, alias, _pool);

                case GetBlockAliasStatus.BlockNotFound:
                    return new GetBlockHandleResponse(ref request.MessageHeader, GetBlockHandleStatus.BlockNotFound,
                        _pool);

                default:
                    throw new NotSupportedException();
            }
        }

        public CommitBlockResponse CommitBlock(CommitBlockRequest request)
        {
            var status = _store.TryCommitBlock(request.BlockAlias, request.BlockId, out _);

            var requestId = request.MessageHeader.RequestId;
            var clientId = request.MessageHeader.ClientId;

            switch (status)
            {
                case CommitBlockStatus.Success:
                    RefreshAndPropagateLineage();
                    return new CommitBlockResponse(requestId, clientId, Messaging.CommitBlockStatus.Success, _pool);

                case CommitBlockStatus.BlockNotFound:
                    return new CommitBlockResponse(requestId, clientId, Messaging.CommitBlockStatus.BlockNotFound,
                        _pool);

                case CommitBlockStatus.BlockIdMismatch:
                    return new CommitBlockResponse(requestId, clientId, Messaging.CommitBlockStatus.BlockIdMismatch,
                        _pool);

                default:
                    throw new NotSupportedException();
            }
        }

        public Span<byte> GetBlockInfo(GetBlockInfoRequest request)
        {
            // the request is the same for committed and uncommitted blocks
            // which is why one needs to look for it among the committed and
            // uncommitted blocks of the blockchain.

            if (_store.TryGetCommittedBlock(request.BlockAlias, out var committedBlock))
            {
                return new GetBlockInfoResponse(
                    ref request.MessageHeader,
                    committedBlock.BlockId,
                    committedBlock.Alias,
                    committedBlock.Parent,
                    committedBlock.BlockHeight,
                    request.Mask,
                    _pool).Span;
            }

            _store.TryGetUncommittedBlock(request.BlockAlias, out var uncommittedBlock);

            return new GetBlockInfoResponse(
                ref request.MessageHeader,
                uncommittedBlock.BlockId,
                uncommittedBlock.Alias,
                uncommittedBlock.Parent,
                uncommittedBlock.BlockHeight,
                request.Mask,
                _pool).Span;
        }
    }
}