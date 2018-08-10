using System;
using Terab.UTXO.Core.Helpers;

namespace Terab.UTXO.Core.Hash
{
    /// <summary>
    /// This class provides methods for bit manipulation in big endian order.
    /// This is useful for truncating bit sequences while keeping the most
    /// important bits.
    /// </summary>
    /// <remarks>
    /// It has been obsoleted by the standard class
    /// System.Buffers.Binary.BinaryPrimitives.
    /// </remarks>
    public static class BigEndianConverter
    {
        public static bool TryWriteBytes(Span<byte> destination, ulong value)
        {
            return System.Buffers.Binary.BinaryPrimitives.TryWriteUInt64BigEndian(destination, value);
        }

        public static void WriteBytes(ref SpanBinaryEditor destination, ulong value)
        {
            destination.WriteUInt64BE(value);
        }

        public static ulong ToUInt64(ReadOnlySpan<byte> buffer)
        {
            return System.Buffers.Binary.BinaryPrimitives.ReadUInt64BigEndian(buffer);
        }

        public static ulong ReadUInt64(ref SpanBinaryReader buffer)
        {
            return buffer.ReadUInt64BE();
        }

    }
}
