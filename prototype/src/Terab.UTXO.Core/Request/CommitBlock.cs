using Terab.UTXO.Core.Blockchain;
using Terab.UTXO.Core.Messaging;
using Terab.UTXO.Core.Response;

namespace Terab.UTXO.Core.Request
{
    /// <summary>
    /// Request to render the block identified by the handle
    /// non-modifiable, meaning no more
    /// transactions can be added to that block.
    /// The ID that will be associated with the committed block
    /// has to be provided in the request.
    /// The response to that request <see cref="EverythingOkResponse"/>.
    /// </summary>
    public class CommitBlock : RequestBase
    {
        public static readonly MessageInfo Info =
            new MessageInfo(false, SizeInBytes, false, MessageType.CommitBlock, "CommitBlock");

        public BlockAlias BlockHandle { get; }

        public BlockId BlockId { get; }

        public static int SizeInBytes => BaseSizeInBytes + sizeof(int) + BlockId.SizeInBytes;

        public CommitBlock(uint requestId, uint clientId, BlockAlias blockHandle, BlockId blockId) :
            base(requestId, clientId, MessageType.CommitBlock)
        {
            BlockHandle = blockHandle;
            BlockId = blockId;
        }
    }
}
