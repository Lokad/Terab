using Terab.UTXO.Core.Blockchain;
using Terab.UTXO.Core.Hash;
using Terab.UTXO.Core.Messaging;
using Terab.UTXO.Core.Request;

namespace Terab.UTXO.Core.Response
{
    /// <summary>
    /// Response to the <see cref="GetBlockInformation"/> request in case
    /// the alias transmitted in the request corresponded to a committed block.
    /// </summary>
    public class CommittedBlockInformation : ResponseBase
    {
        public static readonly MessageInfo Info =
            new MessageInfo(false, SizeInBytes, true, MessageType.CommittedBlockInfo, 
                "CommittedBlockInformation");

        public BlockId BlockId { get; }

        public BlockAlias Alias { get; }

        public BlockAlias Parent { get; }

        public int BlockHeight { get; }

        public static int SizeInBytes => BaseSizeInBytes + BlockId.SizeInBytes + sizeof(int) * 3;

        public CommittedBlockInformation(
            uint requestId, 
            uint clientId,
            BlockId blockId,
            BlockAlias alias, 
            int height,
            BlockAlias parent) :
            base(requestId, clientId, MessageType.CommittedBlockInfo)
        {
            BlockHeight = height;
            BlockId = blockId;
            Alias = alias;
            Parent = parent;
        }
    }
}
