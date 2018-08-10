using Terab.UTXO.Core.Blockchain;
using Terab.UTXO.Core.Hash;
using Terab.UTXO.Core.Messaging;
using Terab.UTXO.Core.Request;

namespace Terab.UTXO.Core.Response
{
    /// <summary>
    /// Response to <see cref="GetBlockInformation"/> in case the alias
    /// transmitted in the request corresponded to an uncommitted block.
    /// </summary>
    public class UncommittedBlockInformation : ResponseBase
    {
        public static readonly MessageInfo Info =
            new MessageInfo(false, SizeInBytes, true, MessageType.UncommittedBlockInfo, 
                "UncommittedBlockInformation");

        public TempBlockId UncommittedBlockId { get; }

        public BlockAlias Alias { get; }

        public BlockAlias Parent { get; }

        public int BlockHeight { get; }

        public static int SizeInBytes => BaseSizeInBytes + TempBlockId.SizeInBytes + sizeof(int) * 3;

        public UncommittedBlockInformation(
            uint requestId, 
            uint clientId,
            TempBlockId uncommittedBlockId,
            BlockAlias alias, 
            int height,
            BlockAlias parent) :
            base(requestId, clientId, MessageType.UncommittedBlockInfo)
        {
            BlockHeight = height;
            UncommittedBlockId = uncommittedBlockId;
            Alias = alias;
            Parent = parent;
        }
    }
}
