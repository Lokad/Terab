using Terab.UTXO.Core.Blockchain;
using Terab.UTXO.Core.Hash;
using Terab.UTXO.Core.Messaging;
using Terab.UTXO.Core.Response;

namespace Terab.UTXO.Core.Request
{
    /// <summary>
    /// Requests the handle to an uncommitted block identified by its
    /// temporary ID that was associated with it when it was first opened.
    /// The corresponding response is <see cref="BlockHandleResponse"/>.
    /// </summary>
    public class GetUncommittedBlockHandle : RequestBase
    {
        public static readonly MessageInfo Info =
            new MessageInfo(false, SizeInBytes, false, MessageType.GetUncommittedBlockHandle, 
                "GetUncommittedBlockHandle");

        public TempBlockId UncommittedBlockId { get; }

        public static int SizeInBytes => BaseSizeInBytes + TempBlockId.SizeInBytes;

        public GetUncommittedBlockHandle(uint requestId, uint clientId, TempBlockId uncommittedBlockId) :
            base(requestId, clientId, MessageType.GetUncommittedBlockHandle)
        {
            UncommittedBlockId = uncommittedBlockId;
        }
    }
}
