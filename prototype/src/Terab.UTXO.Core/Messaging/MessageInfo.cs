using System;
using System.Diagnostics;
using Terab.UTXO.Core.Request;
using Terab.UTXO.Core.Response;

namespace Terab.UTXO.Core.Messaging
{
    /// <summary>
    /// Groups all information interesting to know about any given message type.
    /// </summary>
    [DebuggerDisplay("{Name}")]
    public class MessageInfo
    {
        /// <summary>
        /// Indicates whether a message is part of the sharded messages or not.
        /// </summary>
        /// <remarks>
        /// Sharded messages are the ones concerning the Sozu table while
        /// non-sharded messages are about the Terab application itself
        /// (Authenticate) or about the blockchain.
        /// </remarks>
        public bool Sharded { get; }

        /// <summary>
        /// Indicates the minimal message length, important to know to
        /// determine the correct size of (de)serialization buffers, for
        /// instance.
        /// Most messages are of fixed length so the MinLength is the actual
        /// length. However, for messages of variable size (anything including
        /// a bitcoin script), this value will reflect the minimum required
        /// length of the message.
        /// </summary>
        public int MinSizeInBytes { get; }

        /// <summary>
        /// Indicates whether the given message is a client request or a
        /// response from the Terab application.
        /// </summary>
        public bool IsResponse { get; }

        /// <summary>
        /// Represents the message that is described in the instance of this
        /// class. The int corresponds to the enum which is to be found
        /// in <see cref="Messaging.MessageType"/>.
        /// </summary>
        public MessageType MessageType { get; }

        /// <summary>
        /// To make it clear for human users too what message is represented
        /// in a given instance, a name field is at disposition for more
        /// readability.
        /// </summary>
        public string Name { get; }

        public MessageInfo(bool sharded, int minSizeInBytes, bool isResponse, MessageType messageType,
            string name)
        {
            Sharded = sharded;
            MinSizeInBytes = minSizeInBytes;
            IsResponse = isResponse;
            MessageType = messageType;
            Name = name;
        }

        public static MessageInfo FromType(MessageType messageType)
        {
            switch (messageType)
            {
                case MessageType.Authenticate:
                    return Authenticate.Info;
                case MessageType.OpenBlock:
                    return OpenBlock.Info;
                case MessageType.GetBlockHandle:
                    return GetBlockHandle.Info;
                case MessageType.GetUncommittedBlockHandle:
                    return GetUncommittedBlockHandle.Info;
                case MessageType.GetBlockInfo:
                    return GetBlockInformation.Info;
                case MessageType.CommitBlock:
                    return CommitBlock.Info;
                case MessageType.IsAncestor:
                    return IsAncestor.Info;
                case MessageType.IsPruneable:
                    return IsPruneable.Info;
                case MessageType.Authenticated:
                    return Authenticated.Info;
                case MessageType.EverythingOk:
                    return EverythingOkResponse.Info;
                case MessageType.BlockHandle:
                    return BlockHandleResponse.Info;
                case MessageType.AncestorResponse:
                    return AncestorResponse.Info;
                case MessageType.PruneableResponse:
                    return PruneableResponse.Info;
                case MessageType.UncommittedBlockInfo:
                    return UncommittedBlockInformation.Info;
                case MessageType.CommittedBlockInfo:
                    return CommittedBlockInformation.Info;
                case MessageType.OpenedBlock:
                    return OpenedBlock.Info;
                default:
                    throw new NotSupportedException(nameof(messageType));
            }
        }
    }
}