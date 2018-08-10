using Terab.UTXO.Core.Blockchain;
using Terab.UTXO.Core.Messaging;

namespace Terab.UTXO.Core.Request
{
    /// <summary>
    /// Asks for opening a block with a given ID as child of a
    /// given parent.
    /// The response is a message indicating the temporary ID given
    /// to the block and its alias..
    /// </summary>
    public class OpenBlock : RequestBase
    {
        public static readonly MessageInfo Info =
            new MessageInfo(false, SizeInBytes, false, MessageType.OpenBlock, "OpenBlock");

        public BlockAlias ParentHandle { get; }

        public static int SizeInBytes => BaseSizeInBytes + sizeof(int);

        public OpenBlock(uint requestId, uint clientId, BlockAlias parentHandle) : 
            base(requestId, clientId, MessageType.OpenBlock)
        {
            ParentHandle = parentHandle;
        }
    }
}
