using Terab.UTXO.Core.Blockchain;
using Terab.UTXO.Core.Hash;
using Terab.UTXO.Core.Messaging;
using Terab.UTXO.Core.Request;

namespace Terab.UTXO.Core.Response
{
    /// <summary>
    /// Response to <see cref="OpenBlock"/> request. Indicates
    /// that a block has successfully been opened and transmits
    /// information to permit identifying this block in subsequent requests.
    /// </summary>
    public class OpenedBlock : ResponseBase
    {
        public static readonly MessageInfo Info =
            new MessageInfo(false, SizeInBytes, true, MessageType.OpenedBlock, "OpenedBlock");

        public BlockAlias Alias { get; }

        public TempBlockId UncommittedBlockId { get; }

        public static int SizeInBytes => BaseSizeInBytes + TempBlockId.SizeInBytes + sizeof(int);

        public OpenedBlock(
            uint requestId, 
            uint clientId, 
            TempBlockId uncommittedBlockId, 
            BlockAlias alias) 
            : base(requestId, clientId, MessageType.OpenedBlock)
        {
            UncommittedBlockId = uncommittedBlockId;
            Alias = alias;
        }
    }
}
