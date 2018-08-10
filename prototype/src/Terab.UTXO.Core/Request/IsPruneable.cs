using Terab.UTXO.Core.Blockchain;
using Terab.UTXO.Core.Messaging;

namespace Terab.UTXO.Core.Request
{
    /// <summary>
    /// Requests whether a block is pruneable.
    /// The corresponding response is <see cref="BooleanResponse"/>.
    /// </summary>
    public class IsPruneable : RequestBase
    {
        public static readonly MessageInfo Info =
            new MessageInfo(false, SizeInBytes, false, MessageType.IsPruneable, "IsPruneable");

        public BlockAlias BlockHandle { get; }

        public static int SizeInBytes => BaseSizeInBytes + sizeof(int);

        public IsPruneable(uint requestId, uint clientId, BlockAlias blockHandle) :
            base(requestId, clientId, MessageType.IsPruneable)
        {
            BlockHandle = blockHandle;
        }
    }
}
