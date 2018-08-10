using System;
using System.IO;

namespace Terab.UTXO.Core.Helpers
{
    /// <summary>
    /// Supports 7 bit encoding of ints into a stream or a span, and
    /// reading out of the encoded ints likewise.
    /// Mainly needed for <see cref="PayloadReader"/>.
    /// </summary>
    public static class SevenBitEncodingHelpers
    {
        // Binary Reader / Writer methods

        public static int Read7BitEncodedInt(this BinaryReader reader)
        {
            // Read out an Int32 7 bits at a time.  The high bit
            // of the byte when on means to continue reading more bytes.
            var count = 0;
            var shift = 0;
            byte b;

            do
            {
                // Check for a corrupted stream.  Read a max of 5 bytes.
                // In a future version, add a DataFormatException.
                if (shift == 5 * 7)  // 5 bytes max per Int32, shift += 7
                    throw new FormatException("Format_Bad7BitInt32");

                // Handles end of stream cases for us.
                int bb = reader.ReadByte();

                if (bb == -1)
                    throw new FormatException("End of file reached.");

                b = (byte)bb;

                count |= (b & 0x7F) << shift;
                shift += 7;

            } while ((b & 0x80) != 0);

            return count;
        }

        public static int Read7BitEncodedInt(this ref SpanBinaryEditor reader)
        {
            // Read out an Int32 7 bits at a time.  The high bit
            // of the byte when on means to continue reading more bytes.
            int count = 0;
            int shift = 0;
            byte b;

            do
            {
                // Check for a corrupted stream.  Read a max of 5 bytes.
                // In a future version, add a DataFormatException.
                if (shift == 5 * 7)  // 5 bytes max per Int32, shift += 7
                    throw new FormatException("Format_Bad7BitInt32");

                b = reader.ReadByte();

                count |= (b & 0x7F) << shift;
                shift += 7;

            } while ((b & 0x80) != 0);

            return count;
        }

        public static void Write7BitEncodedInt(this BinaryWriter writer, int value)
        {
            // Write out an int 7 bits at a time.  The high bit of the byte,
            // when on, tells reader to continue reading more bytes.
            uint v = (uint)value;   // support negative numbers

            while (v >= 0x80)
            {
                writer.Write((byte)(v | 0x80));
                v >>= 7;
            }
            writer.Write((byte)v);
        }

        public static void Write7BitEncodedInt(this ref SpanBinaryEditor writer, int value)
        {
            // Write out an int 7 bits at a time.  The high bit of the byte,
            // when on, tells reader to continue reading more bytes.
            uint v = (uint)value;   // support negative numbers

            while (v >= 0x80)
            {
                writer.WriteByte((byte)(v | 0x80));
                v >>= 7;
            }
            writer.WriteByte((byte)v);
        }

        // BitConverter methods

        public static int Write7BitEncodedInt(Span<byte> toWrite, int value)
        {
            // Write out an int 7 bits at a time.  The high bit of the byte,
            // when on, tells reader to continue reading more bytes.
            uint v = (uint)value;   // support negative numbers

            var index = 0; // keep track of where to write
            while (v >= 0x80)
            {
                toWrite[index] = (byte)(v | 0x80);
                v >>= 7;
                index++;
            }
            toWrite[index] = (byte)v;

            return index + 1;
        }

        public static (int, int) Read7BitEncodedInt(ReadOnlySpan<byte> toRead)
        {
            // Read out an Int32 7 bits at a time.  The high bit
            // of the byte when on means to continue reading more bytes.
            var count = 0;
            var shift = 0;
            byte b;
            var index = 0;

            do
            {
                // Check for a corrupted stream.  Read a max of 5 bytes.
                if (shift == 5 * 7)  // 5 bytes max per Int32, shift += 7
                    throw new FormatException("Format_Bad7BitInt32");

                // Handles end of stream cases for us.
                b = toRead[index];

                count |= (b & 0x7F) << shift;
                shift += 7;
                index++;

            } while ((b & 0x80) != 0);

            return (count, index);
        }
    }
}
