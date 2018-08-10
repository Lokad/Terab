using System;
using Terab.UTXO.Core.Blockchain;
using Terab.UTXO.Core.Messaging;
using Terab.UTXO.Core.Response;

namespace Terab.Server
{
    /// <summary>
    /// This thread takes into account all requests that are not sharded. These
    /// requests are only allowed to modify and query the current state of the
    /// blockchain, but should not concern the UTX(O) set.
    /// </summary>
    public class ControllerThread
    {
        /// <summary>
        /// The blockchain that the ControllerThread is going to base its
        /// responses on and that he will modify according to requests.
        /// </summary>
        private readonly SimpleBlockchain _chain;

        /// <summary>
        /// The optimized blockchain that the ControllerThread is going to base
        /// its responses on.
        /// TODO: should this be modifiable so that the thread has the latest information?
        /// </summary>
        private readonly OptimizedLineage _opti;

        /// <summary>
        /// Inbox where the <see cref="Dispatcher"/> will write client requests
        /// into.
        /// </summary>
        private readonly BoundedInbox _inbox;

        /// <summary>
        /// Outbox where the ControllerThread will write the responses
        /// to the requests into, which will be sent to the corresponding client
        /// by the <see cref="Dispatcher"/>.
        /// </summary>
        private readonly BoundedInbox _outbox;

        private readonly byte[] _responseBuffer = new byte[CommittedBlockInformation.SizeInBytes];

        public ControllerThread(SimpleBlockchain chain, OptimizedLineage opti, BoundedInbox inbox, BoundedInbox outbox)
        {
            _chain = chain;
            _opti = opti;
            _inbox = inbox;
            _outbox = outbox;
        }

        public void Loop() 
            // TODO: [vermorel] intent and design should be clarified here
        {
            while (true)
            {
                DoWork(); 
                // TODO: [vermorel] don't loop at full speed if nothing gets down,
                // we should have a Monitor pattern or something similar
            }
        }

        public void DoWork() 
        // TODO: [vermorel] method should return 'true' if any work has been done
        {
            var nextMessage = _inbox.Peek();

            if (nextMessage.Length != 0)
            {
                // get message type and deserialize accordingly
                var reqType = ClientServerMessage.GetMessageType(nextMessage);
                switch (reqType)
                {
// TODO: [vermorel] I don't like to too much the code below, it's highly repetitive, factorization is needed.

                    case MessageType.OpenBlock:
                        var openBlock = MessageSerializers.DeserializeOpenBlock(nextMessage);
                        UncommittedBlock block;
                        if (openBlock.ParentHandle == BlockAlias.GenesisParent && _chain.BlockchainLength == 0)
                            block = _chain.OpenFirstBlock();
                        else block = _chain.OpenBlock(openBlock.ParentHandle);
                        var blockHandle = 
                            new OpenedBlock(openBlock.RequestId, openBlock.ClientId, block.BlockId, block.Alias);
                        MessageSerializers.SerializeOpenedBlock(blockHandle, _responseBuffer);
                        _outbox.TryWrite(new Span<byte>(_responseBuffer, 0, OpenedBlock.SizeInBytes));
                        break;
                    case MessageType.GetBlockHandle:
                        var getBlockHandle = MessageSerializers.DeserializeGetBlockHandle(nextMessage);
                        var retrievedHandle = _chain.RetrieveAlias(getBlockHandle.BlockId);
                        var returnMessage = new BlockHandleResponse(getBlockHandle.RequestId, getBlockHandle.ClientId,
                            retrievedHandle);
                        MessageSerializers.SerializeBlockHandleResponse(returnMessage, _responseBuffer);
                        _outbox.TryWrite(new Span<byte>(_responseBuffer, 0, BlockHandleResponse.SizeInBytes));
                        break;
                    case MessageType.GetUncommittedBlockHandle:
                        var getUBlockHandle = MessageSerializers.DeserializeGetUncommittedBlockHandle(nextMessage);
                        var retrievedUHandle = _chain.RetrieveAlias(getUBlockHandle.UncommittedBlockId);
                        var returnMes = new BlockHandleResponse(getUBlockHandle.RequestId, getUBlockHandle.ClientId,
                            retrievedUHandle);
                        MessageSerializers.SerializeBlockHandleResponse(returnMes, _responseBuffer);
                        _outbox.TryWrite(new Span<byte>(_responseBuffer, 0, BlockHandleResponse.SizeInBytes));
                        break;
                    case MessageType.CommitBlock:
                        var commitBlock = MessageSerializers.DeserializeCommitBlock(nextMessage);
                        _chain.CommitBlock(commitBlock.BlockHandle, commitBlock.BlockId);
                        var committedMessage =
                            new EverythingOkResponse(commitBlock.RequestId, commitBlock.ClientId);
                        MessageSerializers.SerializeEverythingOk(committedMessage, _responseBuffer);
                        _outbox.TryWrite(new Span<byte>(_responseBuffer, 0, EverythingOkResponse.SizeInBytes));
                        break;
                    case MessageType.IsAncestor:
                        var isAncestor = MessageSerializers.DeserializeIsAncestor(nextMessage);
                        var res = _opti.IsAncestor(isAncestor.BlockHandle, isAncestor.MaybeAncestorHandle);
                        var isAncestorMessage = new AncestorResponse(isAncestor.RequestId, isAncestor.ClientId, res);
                        MessageSerializers.SerializeAncestorResponse(isAncestorMessage, _responseBuffer);
                        _outbox.TryWrite(new Span<byte>(_responseBuffer, 0, AncestorResponse.SizeInBytes));
                        break;
                    case MessageType.IsPruneable:
                        var isPruneable = MessageSerializers.DeserializeIsPruneable(nextMessage);
                        var result = _opti.IsPruneable(isPruneable.BlockHandle);
                        var isPruneableMessage = new PruneableResponse(isPruneable.RequestId, isPruneable.ClientId, result);
                        MessageSerializers.SerializePruneableResponse(isPruneableMessage, _responseBuffer);
                        _outbox.TryWrite(new Span<byte>(_responseBuffer, 0, PruneableResponse.SizeInBytes));
                        break;
                    case MessageType.GetBlockInfo:
                        var blockInfoReq = MessageSerializers.DeserializeGetBlockInfo(nextMessage);

                        // TODO: [vermorel] clarify the purpose of this test, unclear
                        if (_chain.RetrieveCommittedBlock(blockInfoReq.BlockHandle, out var blockInfoC))
                        {
                            var blockInfo = 
                                new CommittedBlockInformation(blockInfoReq.RequestId, blockInfoReq.ClientId, 
                                    blockInfoC.BlockId, blockInfoC.Alias, blockInfoC.BlockHeight, blockInfoC.Parent);
                            MessageSerializers.SerializeCommittedBlockInfo(blockInfo, _responseBuffer);
                            _outbox.TryWrite(new Span<byte>(_responseBuffer, 0, CommittedBlockInformation.SizeInBytes));
                            break;
                        }
                        else
                        {
                            _chain.RetrieveUncommittedBlock(blockInfoReq.BlockHandle, out var blockInfoU);
                            var blockInfo = 
                                new UncommittedBlockInformation(blockInfoReq.RequestId, blockInfoReq.ClientId, 
                                    blockInfoU.BlockId, blockInfoU.Alias, blockInfoU.BlockHeight, blockInfoU.Parent);
                            MessageSerializers.SerializeUncommittedBlockInfo(blockInfo, _responseBuffer);
                            _outbox.TryWrite(new Span<byte>(_responseBuffer, 0, UncommittedBlockInformation.SizeInBytes));
                            break;
                        }

// TODO: [vermorel] the 'default:' case should be covered with an NotSupportedException.
                }
                _inbox.Next();
                // TODO: errors are not yet handled.
            }
        }
    }
}
