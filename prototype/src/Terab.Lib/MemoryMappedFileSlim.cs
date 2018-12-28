// Copyright Lokad 2018 under MIT BCH.
using System;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;

namespace Terab.Lib
{
    /// <summary>
    /// Zero-copy access to a memory mapped file through 'Span' accessor.
    /// </summary>
    /// <remarks>
    /// The purpose of this class is to offer the possibility to perform
    /// I/O over memory mapped files with zero copy and zero object allocation.
    /// </remarks>
    public sealed unsafe class MemoryMappedFileSlim : IDisposable
    {
        private readonly string _fileName;

        private readonly long _fileLength;

        private readonly MemoryMappedFile _mmf;

        private readonly MemoryMappedViewAccessor _mmva;

        private readonly byte* _originPtr = null;

        private bool _disposed;

        public string FileName => _fileName;

        public long FileLength => _fileLength;

        private long _lastOffset;
        private int _lastLength;

        public MemoryMappedFileSlim(string fileNamePath)
        {
            _fileName = fileNamePath;
            _mmf = MemoryMappedFile.CreateFromFile(fileNamePath, FileMode.Open, mapName: null);
            _mmva = _mmf.CreateViewAccessor();
            _fileLength = _mmva.Capacity;
            _mmva.SafeMemoryMappedViewHandle.AcquirePointer(ref _originPtr);
            _disposed = false;
        }

        public MemoryMappedFileSlim(string fileNamePath, long fileLength)
        {
            _fileName = fileNamePath;
            _fileLength = fileLength;
            _mmf = MemoryMappedFile.CreateFromFile(fileNamePath, FileMode.OpenOrCreate, mapName: null,
                capacity: fileLength);
            _mmva = _mmf.CreateViewAccessor();
            _mmva.SafeMemoryMappedViewHandle.AcquirePointer(ref _originPtr);
            _disposed = false;
        }

        public MemoryMappedFileSlim(MemoryMappedFile mmf)
        {
            _fileName = "Undefined file name.";
            _mmf = mmf;
            _mmva = _mmf.CreateViewAccessor();
            _mmva.SafeMemoryMappedViewHandle.AcquirePointer(ref _originPtr);
            _disposed = false;
            _fileLength = _mmva.Capacity;
        }

        public Span<byte> GetSpan()
        {
            if (_fileLength >= int.MaxValue)
                throw new InvalidOperationException("File too large.");

            return new Span<byte>(_originPtr, (int) _fileLength);
        }

        public Span<byte> GetSpan(long offset, int length)
        {
            if (_disposed)
                throw new InvalidOperationException();

            if (offset < 0 || offset > _fileLength)
                throw new ArgumentOutOfRangeException(nameof(offset));

            if (length < 0 || offset + length > _fileLength)
                throw new ArgumentOutOfRangeException(nameof(length));

            _lastOffset = offset;
            _lastLength = length;

            return new Span<byte>(_originPtr + offset, length);
        }

        /// <summary>
        /// Clears all buffers for this view and causes any buffered data to be written to the underlying file.
        /// </summary>
        public void Flush()
        {
            _mmva.Flush();
        }

        /// <summary>
        /// Flush restricted to the region covered by the last call to 'GetSpan'.
        /// </summary>
        public void FlushLastSpan()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                // Based on the underlying implementation of MemoryMappedFile
                // See https://github.com/dotnet/corefx/blob/master/src/System.IO.MemoryMappedFiles/src/System/IO/MemoryMappedFiles/MemoryMappedView.Unix.cs
                MyInterop.Sys.MSync((IntPtr) (_originPtr + _lastOffset), (ulong) _lastLength,
                    MyInterop.Sys.MemoryMappedSyncFlags.MS_SYNC | MyInterop.Sys.MemoryMappedSyncFlags.MS_INVALIDATE);

                return;
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // Based on the underlying implementation of MemoryMappedFile
                // See https://github.com/dotnet/corefx/blob/master/src/System.IO.MemoryMappedFiles/src/System/IO/MemoryMappedFiles/MemoryMappedView.Windows.cs
                MyInterop.Kernel32.FlushViewOfFile((IntPtr) (_originPtr + _lastOffset), (UIntPtr) _lastLength);

                // See https://docs.microsoft.com/en-us/windows/desktop/FileIO/file-buffering
                // See https://docs.microsoft.com/en-us/windows/desktop/api/fileapi/nf-fileapi-flushfilebuffers
                MyInterop.Kernel32.FlushFileBuffers(_mmva.SafeMemoryMappedViewHandle);

                return;
            }

            throw new NotSupportedException($"Platform not supported: {RuntimeInformation.OSDescription}.");
        }

        private void ReleaseUnmanagedResources()
        {
            _mmva.SafeMemoryMappedViewHandle.ReleasePointer();
        }

        private void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                ReleaseUnmanagedResources();
                if (disposing)
                {
                    _mmf?.Dispose();
                    _mmva?.Dispose();
                }

                _disposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~MemoryMappedFileSlim()
        {
            Dispose(false);
        }

        /// <summary> Interop helper. </summary>
        internal class MyInterop
        {
            /// <summary> Windows </summary>
            internal class Kernel32
            {
                // See
                // https://github.com/dotnet/corefx/blob/master/src/Common/src/Interop/Windows/Interop.Libraries.cs
                // https://github.com/dotnet/corefx/blob/master/src/Common/src/Interop/Windows/kernel32/Interop.FlushViewOfFile.cs

                [DllImport("kernel32.dll", SetLastError = true)]
                internal extern static bool FlushViewOfFile(IntPtr lpBaseAddress, UIntPtr dwNumberOfBytesToFlush);

                // See
                // https://github.com/dotnet/corefx/blob/master/src/Common/src/CoreLib/Interop/Windows/Kernel32/Interop.FlushFileBuffers.cs
                [DllImport("kernel32.dll", SetLastError = true)]
                [return: MarshalAs(UnmanagedType.Bool)]
                internal extern static bool FlushFileBuffers(SafeHandle hHandle);
            }

            /// <summary> Linux </summary>
            internal static class Sys
            {
                // See 
                // https://github.com/dotnet/corefx/blob/master/src/Common/src/Interop/Unix/Interop.Libraries.cs
                // https://github.com/dotnet/corefx/blob/master/src/Common/src/Interop/Unix/System.Native/Interop.MSync.cs

                [Flags]
                internal enum MemoryMappedSyncFlags
                {
                    MS_ASYNC = 0x1,
                    MS_SYNC = 0x2,
                    MS_INVALIDATE = 0x10,
                }

                [DllImport("System.Native", EntryPoint = "SystemNative_MSync", SetLastError = true)]
                internal static extern int MSync(IntPtr addr, ulong len, MemoryMappedSyncFlags flags);
            }
        }
    }
}