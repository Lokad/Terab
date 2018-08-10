using System;
using Terab.UTXO.Core.Blockchain;
using Terab.UTXO.Core.Hash;
using Terab.UTXO.Core.Helpers;
using Terab.UTXO.Core.Request;
using Terab.UTXO.Core.Response;

namespace Terab.UTXO.Core.Messaging
{
    // TODO: [vermorel] Split 'MessageSerializers' into 'RequestSerializer' and 'ResponseSerializer'.


    /// <summary>
    /// Helper class to serialize messages to be returned to clients and
    /// deserialize messages received by the clients. This is necessary as
    /// communication is done via byte stream.
    /// </summary>
    public static class MessageSerializers
    {
        public static OpenBlock DeserializeOpenBlock(ReadOnlySpan<byte> message)
        {
            var editor = new SpanBinaryReader(message);

            var header = ParseMessageHeader(ref editor, OpenBlock.Info);
            var parentAlias = new BlockAlias(editor.ReadUInt32());

            return new OpenBlock(header.RequestId, header.ClientId, parentAlias);
        }

        public static void ClientSerializeOpenBlock(OpenBlock openBlock, Span<byte> toWrite)
        {
            // length of the given span
            if (toWrite.Length < OpenBlock.SizeInBytes)
                throw new ArgumentException(
                    $"Given Span of length {toWrite.Length} too short to write {OpenBlock.SizeInBytes} bytes of {nameof(OpenBlock)} request");

            var editor = new SpanBinaryEditor(toWrite);

            var messageHeader = new MessageHeader(OpenBlock.SizeInBytes, openBlock.RequestId,
                openBlock.ClientId, MessageInfo.FromType(openBlock.RequestType));

            WriteMessageHeader(ref messageHeader, ref editor);

            editor.WriteUInt32(openBlock.ParentHandle.Value);
        }

        public static GetBlockHandle DeserializeGetBlockHandle(ReadOnlySpan<byte> message)
        {
            var editor = new SpanBinaryReader(message);
            var header = ParseMessageHeader(ref editor, GetBlockHandle.Info);
            var blockId = BlockId.Create(ref editor);

            return new GetBlockHandle(header.RequestId, header.ClientId, blockId);
        }

        public static void ClientSerializeGetBlockHandle(GetBlockHandle getBlockHandle, Span<byte> toWrite)
        {
            if (toWrite.Length < GetBlockHandle.SizeInBytes)
                throw new ArgumentException(
                    $"Given Span of length {toWrite.Length} too short to write {OpenBlock.SizeInBytes} bytes of {nameof(OpenBlock)} request");

            var editor = new SpanBinaryEditor(toWrite);

            var messageHeader = new MessageHeader(GetBlockHandle.SizeInBytes, getBlockHandle.RequestId,
                getBlockHandle.ClientId, MessageInfo.FromType(getBlockHandle.RequestType));

            WriteMessageHeader(ref messageHeader, ref editor);

            getBlockHandle.BlockId.WriteTo(ref editor);
        }

        public static GetUncommittedBlockHandle DeserializeGetUncommittedBlockHandle(ReadOnlySpan<byte> message)
        {
            var editor = new SpanBinaryReader(message);
            var header = ParseMessageHeader(ref editor, GetUncommittedBlockHandle.Info);
            var uncommittedBlockId = TempBlockId.Create(ref editor);

            return new GetUncommittedBlockHandle(header.RequestId, header.ClientId, uncommittedBlockId);
        }

        public static void ClientSerializeGetUncommittedBlockHandle(GetUncommittedBlockHandle getUncommittedBlockHandle,
            Span<byte> toWrite)
        {
            var editor = new SpanBinaryEditor(toWrite);

            var messageHeader = new MessageHeader(GetUncommittedBlockHandle.SizeInBytes,
                getUncommittedBlockHandle.RequestId, getUncommittedBlockHandle.ClientId,
                MessageInfo.FromType(getUncommittedBlockHandle.RequestType));

            WriteMessageHeader(ref messageHeader, ref editor);

            getUncommittedBlockHandle.UncommittedBlockId.WriteTo(ref editor);
        }

        public static CommitBlock DeserializeCommitBlock(ReadOnlySpan<byte> message)
        {
            var editor = new SpanBinaryReader(message);

            var header = ParseMessageHeader(ref editor, CommitBlock.Info);

            var blockHandle = new BlockAlias(editor.ReadUInt32());
            var blockId = BlockId.Create(ref editor);

            return new CommitBlock(header.RequestId, header.ClientId, blockHandle, blockId);
        }

        public static void ClientSerializeCommitBlock(CommitBlock commitBlock, Span<byte> toWrite)
        {
            var editor = new SpanBinaryEditor(toWrite);

            var messageHeader = new MessageHeader(CommitBlock.SizeInBytes, commitBlock.RequestId,
                commitBlock.ClientId, MessageInfo.FromType(commitBlock.RequestType));

            WriteMessageHeader(ref messageHeader, ref editor);

            editor.WriteUInt32(commitBlock.BlockHandle.Value);
            commitBlock.BlockId.WriteTo(ref editor);
        }

        public static GetBlockInformation DeserializeGetBlockInfo(ReadOnlySpan<byte> message)
        {
            var editor = new SpanBinaryReader(message);
            var header = ParseMessageHeader(ref editor, GetBlockInformation.Info);

            var blockHandle = new BlockAlias(editor.ReadUInt32());

            return new GetBlockInformation(header.RequestId, header.ClientId, blockHandle);
        }

        public static void ClientSerializeGetBlockInformation(GetBlockInformation getInfo, in Span<byte> toWrite)
        {
            if (toWrite.Length < GetBlockInformation.SizeInBytes)
                throw new ArgumentException(
                    $"Given Span of length {toWrite.Length} too short to write {GetBlockInformation.SizeInBytes} bytes of {nameof(GetBlockInformation)} request");

            var editor = new SpanBinaryEditor(toWrite);

            var messageHeader = new MessageHeader(GetBlockInformation.SizeInBytes, getInfo.RequestId,
                getInfo.ClientId, MessageInfo.FromType(getInfo.RequestType));

            WriteMessageHeader(ref messageHeader, ref editor);
            editor.WriteUInt32(getInfo.BlockHandle.Value);
        }

        public static IsAncestor DeserializeIsAncestor(ReadOnlySpan<byte> message)
        {
            var editor = new SpanBinaryReader(message);
            var header = ParseMessageHeader(ref editor, IsAncestor.Info);

            var blockHandle = new BlockAlias(editor.ReadUInt32());
            var maybeAncestorHandle = new BlockAlias(editor.ReadUInt32());

            return new IsAncestor(header.RequestId, header.ClientId, blockHandle, maybeAncestorHandle);
        }

        public static void ClientSerializeIsAncestor(IsAncestor isAncestor, in Span<byte> toWrite)
        {
            if (toWrite.Length < IsAncestor.SizeInBytes)
                throw new ArgumentException(
                    $"Given Span of length {toWrite.Length} too short to write {IsAncestor.SizeInBytes} bytes of {nameof(IsAncestor)} request");

            var editor = new SpanBinaryEditor(toWrite);

            var messageHeader = new MessageHeader(IsAncestor.SizeInBytes, isAncestor.RequestId,
                isAncestor.ClientId, MessageInfo.FromType(isAncestor.RequestType));

            WriteMessageHeader(ref messageHeader, ref editor);

            editor.WriteUInt32(isAncestor.BlockHandle.Value);
            editor.WriteUInt32(isAncestor.MaybeAncestorHandle.Value);
        }

        public static IsPruneable DeserializeIsPruneable(ReadOnlySpan<byte> message)
        {
            var editor = new SpanBinaryReader(message);
            var header = ParseMessageHeader(ref editor, IsPruneable.Info);

            var blockHandle = new BlockAlias(editor.ReadUInt32());

            return new IsPruneable(header.RequestId, header.ClientId, blockHandle);
        }

        public static void ClientSerializeIsPruneable(IsPruneable isPruneable, in Span<byte> toWrite)
        {
            if (toWrite.Length < IsPruneable.SizeInBytes)
                throw new ArgumentException(
                    $"Given Span of length {toWrite.Length} too short to write {IsPruneable.SizeInBytes} bytes of {nameof(IsAncestor)} request");

            var editor = new SpanBinaryEditor(toWrite);

            var messageHeader = new MessageHeader(IsPruneable.SizeInBytes, isPruneable.RequestId,
                isPruneable.ClientId, MessageInfo.FromType(isPruneable.RequestType));

            WriteMessageHeader(ref messageHeader, ref editor);

            editor.WriteUInt32(isPruneable.BlockHandle.Value);
        }

        public static void SerializeAncestorResponse(AncestorResponse response, Span<byte> toWrite)
        {
            if (toWrite.Length < AncestorResponse.SizeInBytes)
                throw new ArgumentException(
                    $"Given Span of length {toWrite.Length} too short to write AncestorResponse of {AncestorResponse.SizeInBytes}.");

            var editor = new SpanBinaryEditor(toWrite);

            var messageHeader = new MessageHeader(AncestorResponse.SizeInBytes, response.RequestId,
                response.ClientId, MessageInfo.FromType(response.ResponseType));

            WriteMessageHeader(ref messageHeader, ref editor);

            editor.WriteBoolean(response.Answer);
        }

        public static void SerializePruneableResponse(PruneableResponse response, Span<byte> toWrite)
        {
            if (toWrite.Length < PruneableResponse.SizeInBytes)
                throw new ArgumentException(
                    $"Given Span of length {toWrite.Length} too short to write PruneableResponse of {PruneableResponse.SizeInBytes}.");

            var editor = new SpanBinaryEditor(toWrite);

            var messageHeader = new MessageHeader(PruneableResponse.SizeInBytes, response.RequestId,
                response.ClientId, MessageInfo.FromType(response.ResponseType));

            WriteMessageHeader(ref messageHeader, ref editor);

            editor.WriteBoolean(response.Answer);
        }

        public static AncestorResponse ClientDeserializeAncestorResponse(ReadOnlySpan<byte> message)
        {
            var editor = new SpanBinaryReader(message);

            var header = ParseMessageHeader(ref editor, AncestorResponse.Info);

            var isTrue = editor.ReadBoolean();

            return new AncestorResponse(header.RequestId, header.ClientId, isTrue);
        }

        public static PruneableResponse ClientDeserializePruneableResponse(ReadOnlySpan<byte> message)
        {
            var editor = new SpanBinaryReader(message);

            var header = ParseMessageHeader(ref editor, PruneableResponse.Info);

            var isTrue = editor.ReadBoolean();

            return new PruneableResponse(header.RequestId, header.ClientId, isTrue);
        }

        public static void SerializeBlockHandleResponse(BlockHandleResponse response, Span<byte> toWrite)
        {
            if (toWrite.Length < BlockHandleResponse.SizeInBytes)
                throw new ArgumentException(
                    $"Given Span of length {toWrite.Length} too short to write BlockHandle of {BlockHandleResponse.SizeInBytes}.");

            var editor = new SpanBinaryEditor(toWrite);

            var messageHeader = new MessageHeader(BlockHandleResponse.SizeInBytes, response.RequestId,
                response.ClientId, MessageInfo.FromType(response.ResponseType));

            WriteMessageHeader(ref messageHeader, ref editor);

            editor.WriteUInt32(response.BlockHandle.Value);
        }

        public static BlockHandleResponse ClientDeserializeBlockHandleResponse(ReadOnlySpan<byte> message)
        {
            var editor = new SpanBinaryReader(message);
            var header = ParseMessageHeader(ref editor, BlockHandleResponse.Info);

            var blockHandle = new BlockAlias(editor.ReadUInt32());

            return new BlockHandleResponse(header.RequestId, header.ClientId, blockHandle);
        }

        public static void SerializeEverythingOk(EverythingOkResponse response, Span<byte> toWrite)
        {
            if (toWrite.Length < EverythingOkResponse.SizeInBytes)
                throw new ArgumentException(
                    $"Given Span of length {toWrite.Length} too short to write EverythingOkResponse of {EverythingOkResponse.SizeInBytes}.");

            var editor = new SpanBinaryEditor(toWrite);

            var messageHeader = new MessageHeader(EverythingOkResponse.SizeInBytes, response.RequestId,
                response.ClientId, MessageInfo.FromType(response.ResponseType));

            WriteMessageHeader(ref messageHeader, ref editor);
        }

        public static EverythingOkResponse ClientDeserializeEverythingOk(ReadOnlySpan<byte> message)
        {
            var editor = new SpanBinaryReader(message);
            var header =
                ParseMessageHeader(ref editor, EverythingOkResponse.Info);

            return new EverythingOkResponse(header.RequestId, header.ClientId);
        }

        public static void SerializeOpenedBlock(OpenedBlock response, Span<byte> toWrite)
        {
            if (toWrite.Length < OpenedBlock.SizeInBytes)
                throw new ArgumentException(
                    $"Given Span of length {toWrite.Length} too short to write OpenedBlock information of {OpenedBlock.SizeInBytes}.");

            var editor = new SpanBinaryEditor(toWrite);

            var messageHeader = new MessageHeader(OpenedBlock.SizeInBytes, response.RequestId,
                response.ClientId, MessageInfo.FromType(response.ResponseType));

            WriteMessageHeader(ref messageHeader, ref editor);

            editor.WriteUInt32(response.Alias.Value);

            response.UncommittedBlockId.WriteTo(ref editor);
        }

        public static OpenedBlock ClientDeserializeOpenedBlock(ReadOnlySpan<byte> message)
        {
            var editor = new SpanBinaryReader(message);
            var header = ParseMessageHeader(ref editor, OpenedBlock.Info);

            var alias = new BlockAlias(editor.ReadUInt32());
            var identifier = TempBlockId.Create(ref editor);

            return new OpenedBlock(header.RequestId, header.ClientId, identifier, alias);
        }

        public static void SerializeUncommittedBlockInfo(UncommittedBlockInformation response, Span<byte> toWrite)
        {
            if (toWrite.Length < UncommittedBlockInformation.SizeInBytes)
                throw new ArgumentException(
                    $"Given Span of length {toWrite.Length} too short to write UncommittedBlock information of {UncommittedBlockInformation.SizeInBytes}.");

            var editor = new SpanBinaryEditor(toWrite);
            var messageHeader = new MessageHeader(UncommittedBlockInformation.SizeInBytes, response.RequestId,
                response.ClientId, MessageInfo.FromType(response.ResponseType));

            WriteMessageHeader(ref messageHeader, ref editor);

            response.UncommittedBlockId.WriteTo(ref editor);

            editor.WriteUInt32(response.Alias.Value);
            editor.WriteUInt32(response.Parent.Value);
            editor.WriteInt32(response.BlockHeight);
        }

        public static UncommittedBlockInformation ClientDeserializeUncommittedBlockInfo(ReadOnlySpan<byte> message)
        {
            var editor = new SpanBinaryReader(message);
            var header = ParseMessageHeader(ref editor, UncommittedBlockInformation.Info);

            var uncommittedBlockId = TempBlockId.Create(ref editor);
            var alias = new BlockAlias(editor.ReadUInt32());
            var parent = new BlockAlias(editor.ReadUInt32());
            var blockHeight = editor.ReadInt32();

            return new UncommittedBlockInformation(
                header.RequestId,
                header.ClientId,
                uncommittedBlockId,
                alias,
                blockHeight,
                parent);
        }

        public static void SerializeCommittedBlockInfo(CommittedBlockInformation response, Span<byte> toWrite)
        {
            if (toWrite.Length < CommittedBlockInformation.SizeInBytes)
                throw new ArgumentException(
                    $"Given Span of length {toWrite.Length} too short to write CommittedBlock information of {CommittedBlockInformation.SizeInBytes}.");

            var editor = new SpanBinaryEditor(toWrite);

            var messageHeader = new MessageHeader(CommittedBlockInformation.SizeInBytes, response.RequestId,
                response.ClientId, MessageInfo.FromType(response.ResponseType));

            WriteMessageHeader(ref messageHeader, ref editor);

            response.BlockId.WriteTo(ref editor);

            editor.WriteUInt32(response.Alias.Value);
            editor.WriteUInt32(response.Parent.Value);
            editor.WriteInt32(response.BlockHeight);
        }

        public static CommittedBlockInformation ClientDeserializeCommittedBlockInfo(ReadOnlySpan<byte> message)
        {
            var editor = new SpanBinaryReader(message);
            var header = ParseMessageHeader(ref editor, CommittedBlockInformation.Info);

            var blockId = BlockId.Create(ref editor);
            var alias = new BlockAlias(editor.ReadUInt32());
            var parent = new BlockAlias(editor.ReadUInt32());
            var blockHeight = editor.ReadInt32();

            return new CommittedBlockInformation(
                header.RequestId,
                header.ClientId,
                blockId,
                alias,
                blockHeight,
                parent);
        }

        /// <summary>
        /// Contains all fields that are present at the beginning of any message.
        /// </summary>
        private struct MessageHeader
        {
            public readonly int SizeInBytes;
            public readonly uint RequestId;
            public readonly uint ClientId;
            public readonly MessageInfo MessageInfo;

            public MessageHeader(int sizeInBytes, uint requestId, uint clientId, MessageInfo messageInfo)
            {
                SizeInBytes = sizeInBytes;
                RequestId = requestId;
                ClientId = clientId;
                MessageInfo = messageInfo;
            }
        }

        private static MessageHeader ParseMessageHeader(ref SpanBinaryReader reader, MessageInfo info)
        {
            if (reader.RemainingLength < info.MinSizeInBytes)
                throw new ArgumentException(
                    $"Given message of size {reader.Length}, should be at least {info.MinSizeInBytes}.");

            var size = reader.ReadInt32();
            if (size != info.MinSizeInBytes)
            {
                throw new ArgumentException(
                    $"Message {info.Name} should be of length {info.MinSizeInBytes} but indicates length {size}.");
            }

            var reqId = reader.ReadUInt32();
            var clientId = reader.ReadUInt32();
            reader.SkipByte(); // Will be deleted once the additional bit in the messages will be deleted

            return new MessageHeader(size, reqId, clientId,
                MessageInfo.FromType((MessageType) reader.ReadInt32()));
        }

        private static void WriteResponseHeaderNoSize(ResponseBase message, ref SpanBinaryEditor writer)
        {
            writer.ClearInt32(); // zero size for now
            writer.WriteUInt32(message.RequestId);
            writer.WriteUInt32(message.ClientId);
            writer.ClearByte();
            writer.WriteInt32((int) message.ResponseType);
        }
        private static void WriteRequestHeaderNoSize(RequestBase message, ref SpanBinaryEditor writer)
        {
            writer.ClearInt32(); // skip size for now
            writer.WriteUInt32(message.RequestId);
            writer.WriteUInt32(message.ClientId);
            writer.ClearByte();
            writer.WriteInt32((int) message.RequestType);
        }

        private static void WriteMessageHeader(ref MessageHeader header, ref SpanBinaryEditor writer)
        {
            writer.WriteInt32(header.SizeInBytes);
            writer.WriteUInt32(header.RequestId);
            writer.WriteUInt32(header.ClientId);
            // a byte was reserved to indicate whether a message is sharded or not.
            // As this can be deduced from the messageTypeI, this byte is to be removed in the future
            // as it is no longer needed.
            writer.ClearByte();
            writer.WriteInt32((int) header.MessageInfo.MessageType);
        }
    }
}