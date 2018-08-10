using System;
using System.IO;
using Terab.UTXO.Core.Helpers;
using Xunit;

namespace Terab.UTXO.Core.Tests
{
    public class SevenBitEncodingHelpersTests
    {
        [Fact]
        public void EncodeDecodeInBigSpan()
        {
            var container = new byte[10];
            SevenBitEncodingHelpers.Write7BitEncodedInt(container, 100);
            Assert.Equal((100, 1), SevenBitEncodingHelpers.Read7BitEncodedInt(container));
        }

        [Fact]
        public void EncodeDecodeInSmallSpan()
        {
            var container = new byte[1];
            SevenBitEncodingHelpers.Write7BitEncodedInt(container, 100);
            Assert.Equal((100, 1), SevenBitEncodingHelpers.Read7BitEncodedInt(container));
        }

        [Fact]
        public void EncodeDecodeBigNumberInSpan()
        {
            var container = new byte[5];
            SevenBitEncodingHelpers.Write7BitEncodedInt(container, 1259551277);
            var (number, length) = SevenBitEncodingHelpers.Read7BitEncodedInt(container);
            Assert.Equal(1259551277, number);
            Assert.Equal(5, length);
        }

        [Fact]
        public void EncodeDecodeNegativeBigNumberInSpan()
        {
            var container = new byte[5];
            SevenBitEncodingHelpers.Write7BitEncodedInt(container, -1259551277);
            Assert.Equal((-1259551277, 5), SevenBitEncodingHelpers.Read7BitEncodedInt(container));
        }

        [Fact]
        public void EncodeDecodeInStream()
        {
            var stream = new MemoryStream();
            var writer = new BinaryWriter(stream);
            var reader = new BinaryReader(stream);
            
            writer.Write7BitEncodedInt(100);
            stream.Seek(0, SeekOrigin.Begin);
            Assert.Equal(100, reader.Read7BitEncodedInt());
        }

        [Fact]
        public void EncodeDecodeBigNumberInStream()
        {
            var stream = new MemoryStream();
            var writer = new BinaryWriter(stream);
            var reader = new BinaryReader(stream);

            writer.Write7BitEncodedInt(1259551277);
            stream.Seek(0, SeekOrigin.Begin);
            Assert.Equal(1259551277, reader.Read7BitEncodedInt());
        }

        [Fact]
        public void EncodeDecodeNegativeBigNumberInStream()
        {
            var stream = new MemoryStream();
            var writer = new BinaryWriter(stream);
            var reader = new BinaryReader(stream);

            writer.Write7BitEncodedInt(-1259551277);
            stream.Seek(0, SeekOrigin.Begin);
            Assert.Equal(-1259551277, reader.Read7BitEncodedInt());
        }
        
        [Fact]
        public void EncodeDecodeInSpanEditor()
        {
            var editor = new SpanBinaryEditor(new Span<byte>(new byte[100]));
            
            editor.Write7BitEncodedInt(100);
            Assert.Equal(1, editor.Position);
            editor.Seek(0, SeekOrigin.Begin);
            Assert.Equal(100, editor.Read7BitEncodedInt());
        }

        [Fact]
        public void EncodeDecodeBigNumberInSpanEditor()
        {
            var editor = new SpanBinaryEditor(new Span<byte>(new byte[100]));

            editor.Write7BitEncodedInt(1259551277);
            editor.Seek(0, SeekOrigin.Begin);
            Assert.Equal(1259551277, editor.Read7BitEncodedInt());
        }

        [Fact]
        public void EncodeDecodeNegativeBigNumberInSpanEditor()
        {
            var editor = new SpanBinaryEditor(new Span<byte>(new byte[100]));

            editor.Write7BitEncodedInt(-1259551277);
            editor.Seek(0, SeekOrigin.Begin);
            Assert.Equal(-1259551277, editor.Read7BitEncodedInt());
        }
    }
}
