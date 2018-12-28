// Copyright Lokad 2018 under MIT BCH.
using System;
using System.IO;
using System.Text;
using Xunit;

namespace Terab.Lib.Tests
{
    /// <summary>
    /// Ensure creation of mmf backing file.
    /// </summary>
    public class BackFileFixture
    {
        public const int BufferSize = 4096;

        private long _length;

        private string _tempFile;

        public long Length => _length;

        public BackFileFixture()
        {
            _length = 0x010000000; // 400MB
        }
        public void Init(string tempFolder)
        {
            _tempFile = Path.Combine(tempFolder, "storage.st");

            using (var spanMmf = new MemoryMappedFileSlim(_tempFile, _length))
            {
                // just initialize file 
            }
        }

        public FileStream GetFileStream()
        {
            return new FileStream(_tempFile, FileMode.Open, FileAccess.ReadWrite,
                FileShare.None, BufferSize);
        }

        public MemoryMappedFileSlim GetMemoryMappedFileSlim()
        {
            return new MemoryMappedFileSlim(_tempFile, _length);
        }

    }

    [Collection("SharedFileSystem")]
    public class MemoryMappedFileSlimTests : IClassFixture<BackFileFixture>
    {
        private BackFileFixture _fixture;

        private readonly byte[] _testArray;

        public MemoryMappedFileSlimTests(BackFileFixture fixture, SharedFileSystem fileSystem)
        {
            _fixture = fixture;
            _fixture.Init(fileSystem.CreateNewDirectoryForTest());
            string testString = "Something important !";
            _testArray = Encoding.ASCII.GetBytes(testString);
        }

        [Fact]
        public void Write()
        {
            int position = 4096 * 10; //some random position
            MemoryMappedFileSlim mapping = null;
            try
            {
                mapping = _fixture.GetMemoryMappedFileSlim();
                _testArray.CopyTo(mapping.GetSpan(position, _testArray.Length));
            }
            finally
            {
                mapping?.Dispose();
            }

            using (var fs = _fixture.GetFileStream())
            {
                byte[] temp = new byte[_testArray.Length];
                fs.Seek(position, SeekOrigin.Begin);
                fs.Read(temp, 0, _testArray.Length);
                Assert.Equal(temp, _testArray);
                fs.Close();
            }
        }

        [Fact]
        public void Read()
        {
            int position = 4096 * 11; //some random position

            using (var fs = _fixture.GetFileStream())
            {
                fs.Seek(position, SeekOrigin.Begin);
                fs.Write(_testArray, 0, _testArray.Length);
                fs.Close();
            }

            MemoryMappedFileSlim mapping = null;
            try
            {
                mapping = _fixture.GetMemoryMappedFileSlim();
                var tempSpan = mapping.GetSpan(position, BackFileFixture.BufferSize)
                    .Slice(0, _testArray.Length);
                Assert.True(tempSpan.SequenceEqual(_testArray.AsSpan()));
            }
            finally
            {
                mapping?.Dispose();
            }
            
        }

        /// <summary>
        /// This test asserts that the movable pointer is at correct place in
        /// and _remaining length is correct.
        /// </summary>
        [Fact]
        public void WriteOverflow()
        {
            long position = _fixture.Length - _testArray.Length + 1;
            MemoryMappedFileSlim mapping = null;
            try
            {
                mapping = _fixture.GetMemoryMappedFileSlim();

                Assert.Throws<ArgumentOutOfRangeException>(()=> mapping.GetSpan(position, _testArray.Length));
            }
            finally
            {
                mapping?.Dispose();
            }

        }
    }
}
