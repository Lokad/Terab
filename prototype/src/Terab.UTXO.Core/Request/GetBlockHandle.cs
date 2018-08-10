using Terab.UTXO.Core.Blockchain;
using Terab.UTXO.Core.Hash;
using Terab.UTXO.Core.Messaging;
using Terab.UTXO.Core.Response;

namespace Terab.UTXO.Core.Request
{
    /// <summary>
    /// Requests the Terab internal block handle associated with a block
    /// identified by its id (Hash256). The corresponding response is
    /// <see cref="BlockHandleResponse"/>.
    /// </summary>
    public class GetBlockHandle : RequestBase
    {
        public static readonly MessageInfo Info =
            new MessageInfo(false, SizeInBytes, false, MessageType.GetBlockHandle, "GetBlockHandle");

        public BlockId BlockId { get; }

        public static int SizeInBytes => BaseSizeInBytes + BlockId.SizeInBytes;

        public GetBlockHandle(uint requestId, uint clientId, BlockId blockId) :
            base(requestId, clientId, MessageType.GetBlockHandle)
        {
            BlockId = blockId;
        }
    }
}
