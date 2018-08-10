using Terab.UTXO.Core.Blockchain;
using Terab.UTXO.Core.Messaging;

namespace Terab.UTXO.Core.Request
{
    /// <summary>
    /// Requests whether a block is an ancestor of another one on the current
    /// state of the blockchain. The corresponding response is
    /// <see cref="BooleanResponse"/>.
    /// </summary>
    public class IsAncestor : RequestBase
    {
        public static readonly MessageInfo Info =
            new MessageInfo(false, SizeInBytes, false, MessageType.IsAncestor, "IsAncestor");

        // TODO: [vermorel] upgrade to `BlockHandle` type replacing int32.
        public BlockAlias BlockHandle { get; }

        public BlockAlias MaybeAncestorHandle { get; }

        // TODO: use 'BlockHandle.SizeInBytes' instead 
        public static int SizeInBytes => BaseSizeInBytes + sizeof(int) + sizeof(int);

        public IsAncestor(uint requestId, uint clientId, BlockAlias blockHandle, BlockAlias maybeAncestorHandle) :
            base(requestId, clientId, MessageType.IsAncestor)
        {
            BlockHandle = blockHandle;
            MaybeAncestorHandle = maybeAncestorHandle;
        }
    }
}
