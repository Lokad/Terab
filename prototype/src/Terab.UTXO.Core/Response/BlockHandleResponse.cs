using Terab.UTXO.Core.Blockchain;
using Terab.UTXO.Core.Messaging;
using Terab.UTXO.Core.Request;

namespace Terab.UTXO.Core.Response
{
    /// <summary>
    /// Returns the handle of a requested block to the client. The block handle
    /// can explicitly or implicitly be requested by the client by either of
    /// the requests <see cref="GetBlockHandle"/>,
    /// <see cref="GetUncommittedBlockHandle"/> or <see cref="OpenBlock"/>.
    /// </summary>
    public class BlockHandleResponse : ResponseBase
    {
        public static readonly MessageInfo Info =
            new MessageInfo(false, SizeInBytes, true, MessageType.BlockHandle, "BlockHandle");

        public BlockAlias BlockHandle { get; }

        public static int SizeInBytes => BaseSizeInBytes + sizeof(int);

        public BlockHandleResponse(uint requestId, uint clientId, BlockAlias blockHandle) :
            base(requestId, clientId, MessageType.BlockHandle)
        {
            BlockHandle = blockHandle;
        }
    }
}
