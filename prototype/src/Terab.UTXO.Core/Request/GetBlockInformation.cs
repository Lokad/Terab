using Terab.UTXO.Core.Blockchain;
using Terab.UTXO.Core.Messaging;
using Terab.UTXO.Core.Response;

namespace Terab.UTXO.Core.Request
{
    /// <summary>
    /// Requests the information associated with a block identified
    /// by its handle. The corresponding response is either a
    /// <see cref="UncommittedBlockInformation"/> or a
    /// <see cref="CommittedBlockInformation"/>, depending on whether
    /// the block has been committed or not.
    /// </summary>
    public class GetBlockInformation : RequestBase
    {
        public static readonly MessageInfo Info =
            new MessageInfo(false, SizeInBytes, false, MessageType.GetBlockInfo, "GetBlockInformation");

        // TODO: [vermorel] Block handle be represented by its own named 'struct' (not int32).
        public BlockAlias BlockHandle { get; }

        public static int SizeInBytes => BaseSizeInBytes + sizeof(int);

        public GetBlockInformation(uint requestId, uint clientId, BlockAlias blockHandle) :
            base(requestId, clientId, MessageType.GetBlockInfo)
        {
            BlockHandle = blockHandle;
        }
    }
}
