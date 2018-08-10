using Terab.UTXO.Core.Blockchain;
using Terab.UTXO.Core.Hash;
using Terab.UTXO.Core.Messaging;
using Terab.UTXO.Core.Request;
using Terab.UTXO.Core.Response;
using Xunit;

namespace Terab.UTXO.Core.Tests
{
    public class MessageSerializersTests
    {
        private readonly byte[] _buffer = new byte[300];

        [Fact]
        public void SerializeDeserializeOpenBlock()
        {
            var openBlock = new OpenBlock(3, 4, new BlockAlias(987623));
            MessageSerializers.ClientSerializeOpenBlock(openBlock, _buffer);
            var newOpenBlock = MessageSerializers.DeserializeOpenBlock(_buffer);
            Assert.True(openBlock.ClientId == newOpenBlock.ClientId
                        && openBlock.RequestId == newOpenBlock.RequestId
                        && openBlock.RequestType == newOpenBlock.RequestType
                        && openBlock.ParentHandle == newOpenBlock.ParentHandle);
        }

        [Fact]
        public void SerializeDeserializeGetBlockHandle()
        {
            var getBlockHandle = new GetBlockHandle(3, 4, new BlockId(new Hash256(987654321, 123456789, 567894321, 432156789)));
            MessageSerializers.ClientSerializeGetBlockHandle(getBlockHandle, _buffer);
            var newGetBlockHandle = MessageSerializers.DeserializeGetBlockHandle(_buffer);
            Assert.True(getBlockHandle.ClientId == newGetBlockHandle.ClientId
                        && getBlockHandle.RequestId == newGetBlockHandle.RequestId
                        && getBlockHandle.RequestType == newGetBlockHandle.RequestType
                        && getBlockHandle.BlockId.Equals(newGetBlockHandle.BlockId));
        }

        [Fact]
        public void SerializeDeserializeGetUBlockHandle()
        {
            var getUBlockHandle = new GetUncommittedBlockHandle(3, 4, new TempBlockId(new Hash128(987654321, 123456789)));
            MessageSerializers.ClientSerializeGetUncommittedBlockHandle(getUBlockHandle, _buffer);
            var newGetUBlockHandle = MessageSerializers.DeserializeGetUncommittedBlockHandle(_buffer);
            Assert.True(getUBlockHandle.ClientId == newGetUBlockHandle.ClientId
                        && getUBlockHandle.RequestId == newGetUBlockHandle.RequestId
                        && getUBlockHandle.RequestType == newGetUBlockHandle.RequestType
                        && getUBlockHandle.UncommittedBlockId.Equals(newGetUBlockHandle.UncommittedBlockId));
        }

        [Fact]
        public void SerializeDeserializeCommitBlock()
        {
            var commitBlock = new CommitBlock(3, 4, new BlockAlias(70),
                new BlockId(new Hash256(987654321, 123456789, 567894321, 432156789)));
            MessageSerializers.ClientSerializeCommitBlock(commitBlock, _buffer);
            var newCommitBlock = MessageSerializers.DeserializeCommitBlock(_buffer);
            Assert.True(commitBlock.ClientId == newCommitBlock.ClientId
                        && commitBlock.RequestId == newCommitBlock.RequestId
                        && commitBlock.RequestType == newCommitBlock.RequestType
                        && commitBlock.BlockId.Equals(newCommitBlock.BlockId)
                        && commitBlock.BlockHandle == newCommitBlock.BlockHandle);
        }

        [Fact]
        public void SerializeDeserializeGetBlockInfo()
        {
            var getBlockInfo = new GetBlockInformation(3, 4, new BlockAlias(70));
            MessageSerializers.ClientSerializeGetBlockInformation(getBlockInfo, _buffer);
            var newGetBlockInfo = MessageSerializers.DeserializeGetBlockInfo(_buffer);
            Assert.True(getBlockInfo.ClientId == newGetBlockInfo.ClientId
                        && getBlockInfo.RequestId == newGetBlockInfo.RequestId
                        && getBlockInfo.RequestType == newGetBlockInfo.RequestType
                        && getBlockInfo.BlockHandle == newGetBlockInfo.BlockHandle);
        }

        [Fact]
        public void SerializeDeserializeCommittedBlockInfo()
        {
            var committedBlockInfo =
                new CommittedBlockInformation(3, 4, new BlockId(new Hash256(987654321, 123456789, 567894321, 432156789)),
                    new BlockAlias(70), 700, new BlockAlias(7000));
            MessageSerializers.SerializeCommittedBlockInfo(committedBlockInfo, _buffer);
            var newCommittedBlockInfo = MessageSerializers.ClientDeserializeCommittedBlockInfo(_buffer);
            Assert.True(committedBlockInfo.ClientId == newCommittedBlockInfo.ClientId
                        && committedBlockInfo.RequestId == newCommittedBlockInfo.RequestId
                        && committedBlockInfo.ResponseType == newCommittedBlockInfo.ResponseType
                        && committedBlockInfo.Alias == newCommittedBlockInfo.Alias
                        && committedBlockInfo.BlockHeight == newCommittedBlockInfo.BlockHeight
                        && committedBlockInfo.Parent == newCommittedBlockInfo.Parent
                        && committedBlockInfo.BlockId.Equals(newCommittedBlockInfo.BlockId));
        }

        [Fact]
        public void SerializeDeserializeUCommittedBlockInfo()
        {
            var uncommittedBlockInfo =
                new UncommittedBlockInformation(3, 4, new TempBlockId(new Hash128(987654321, 123456789)), new BlockAlias(70), 700,
                    new BlockAlias(7000));
            MessageSerializers.SerializeUncommittedBlockInfo(uncommittedBlockInfo, _buffer);
            var newUncommittedBlockInfo = MessageSerializers.ClientDeserializeUncommittedBlockInfo(_buffer);
            Assert.True(uncommittedBlockInfo.ClientId == newUncommittedBlockInfo.ClientId
                        && uncommittedBlockInfo.RequestId == newUncommittedBlockInfo.RequestId
                        && uncommittedBlockInfo.ResponseType == newUncommittedBlockInfo.ResponseType
                        && uncommittedBlockInfo.Alias == newUncommittedBlockInfo.Alias
                        && uncommittedBlockInfo.BlockHeight == newUncommittedBlockInfo.BlockHeight
                        && uncommittedBlockInfo.Parent == newUncommittedBlockInfo.Parent
                        && uncommittedBlockInfo.UncommittedBlockId.Equals(newUncommittedBlockInfo.UncommittedBlockId));
        }

        [Fact]
        public void SerializeDeserializeOpenedBlock()
        {
            var openedBlock =
                new OpenedBlock(3, 4, new TempBlockId(new Hash128(987654321, 123456789)), new BlockAlias(326432));
            MessageSerializers.SerializeOpenedBlock(openedBlock, _buffer);
            var newOpenedBlock = MessageSerializers.ClientDeserializeOpenedBlock(_buffer);
            Assert.True(openedBlock.ClientId == newOpenedBlock.ClientId
                        && openedBlock.RequestId == newOpenedBlock.RequestId
                        && openedBlock.ResponseType == newOpenedBlock.ResponseType
                        && openedBlock.Alias == newOpenedBlock.Alias
                        && openedBlock.UncommittedBlockId.Equals(newOpenedBlock.UncommittedBlockId));
        }


        [Fact]
        public void SerializeDeserializeIsAncestor()
        {
            var isAncestor = new IsAncestor(9947832, 89478310, new BlockAlias(702349), new BlockAlias(679));
            MessageSerializers.ClientSerializeIsAncestor(isAncestor, _buffer);
            var newIsAncestor = MessageSerializers.DeserializeIsAncestor(_buffer);
            Assert.True(isAncestor.ClientId == newIsAncestor.ClientId
                        && isAncestor.RequestId == newIsAncestor.RequestId
                        && isAncestor.RequestType == newIsAncestor.RequestType
                        && isAncestor.BlockHandle == newIsAncestor.BlockHandle
                        && isAncestor.MaybeAncestorHandle == newIsAncestor.MaybeAncestorHandle);
        }

        [Fact]
        public void SerializeDeserializeIsPruneable()
        {
            var isPruneable = new IsPruneable(9947832, 89478310, new BlockAlias(702349));
            MessageSerializers.ClientSerializeIsPruneable(isPruneable, _buffer);
            var newIsPruneable = MessageSerializers.DeserializeIsPruneable(_buffer);
            Assert.True(isPruneable.ClientId == newIsPruneable.ClientId
                        && isPruneable.RequestId == newIsPruneable.RequestId
                        && isPruneable.RequestType == newIsPruneable.RequestType
                        && isPruneable.BlockHandle == newIsPruneable.BlockHandle);
        }

        [Fact]
        public void SerializeDeserializeBlockHandleResponse()
        {
            var blockHandleResponse = new BlockHandleResponse(9947832, 89478310, new BlockAlias(702349));
            MessageSerializers.SerializeBlockHandleResponse(blockHandleResponse, _buffer);
            var newBlockHandleResponse = MessageSerializers.ClientDeserializeBlockHandleResponse(_buffer);
            Assert.True(blockHandleResponse.ClientId == newBlockHandleResponse.ClientId
                        && blockHandleResponse.RequestId == newBlockHandleResponse.RequestId
                        && blockHandleResponse.ResponseType == newBlockHandleResponse.ResponseType
                        && blockHandleResponse.BlockHandle == newBlockHandleResponse.BlockHandle);
        }

        [Fact]
        public void SerializeDeserializeAncestorResponse()
        {
            var ancestorResponse = new AncestorResponse(9947832, 89478310, true);
            MessageSerializers.SerializeAncestorResponse(ancestorResponse, _buffer);
            var newAncestorResponse = MessageSerializers.ClientDeserializeAncestorResponse(_buffer);
            Assert.True(ancestorResponse.ClientId == newAncestorResponse.ClientId
                        && ancestorResponse.RequestId == newAncestorResponse.RequestId
                        && ancestorResponse.ResponseType == newAncestorResponse.ResponseType
                        && ancestorResponse.Answer == newAncestorResponse.Answer);
        }

        [Fact]
        public void SerializeDeserializePruneableResponse()
        {
            var pruneableResponse = new PruneableResponse(9947832, 89478310, false);
            MessageSerializers.SerializePruneableResponse(pruneableResponse, _buffer);
            var newpruneableResponse = MessageSerializers.ClientDeserializePruneableResponse(_buffer);
            Assert.True(pruneableResponse.ClientId == newpruneableResponse.ClientId
                        && pruneableResponse.RequestId == newpruneableResponse.RequestId
                        && pruneableResponse.ResponseType == newpruneableResponse.ResponseType
                        && pruneableResponse.Answer == newpruneableResponse.Answer);
        }

        [Fact]
        public void SerializeDeserializeEverythingOk()
        {
            var everythinkOkResponse = new EverythingOkResponse(9947832, 89478310);
            MessageSerializers.SerializeEverythingOk(everythinkOkResponse, _buffer);
            var newEverythingOkResponse = MessageSerializers.ClientDeserializeEverythingOk(_buffer);
            Assert.True(everythinkOkResponse.ClientId == newEverythingOkResponse.ClientId
                        && everythinkOkResponse.RequestId == newEverythingOkResponse.RequestId
                        && everythinkOkResponse.ResponseType == newEverythingOkResponse.ResponseType);
        }
    }
}