using System;
using System.Diagnostics;

namespace Terab.UTXO.Core.Sozu
{
    /// <summary>
    ///     Used as index of Sector. <see cref="Sector" />
    /// </summary>
    public struct SectorIndex
    {
        public static SectorIndex Unknown = new SectorIndex(-1L);
        public static SectorIndex First = new SectorIndex(0);

        public static SectorIndex OffsetInBytes(long byteIndex, int sectorSizeInBytes)
        {
            Debug.Assert(sectorSizeInBytes > 0);
            if (byteIndex % sectorSizeInBytes != 0)
                throw new InvalidOperationException(
                    $"Converting a byte offset of ${byteIndex} that is not a multiple of the size of a sector (${sectorSizeInBytes} bytes)"
                );

            return new SectorIndex(byteIndex / sectorSizeInBytes);
        }

        /// <summary>
        /// The wrapped index of the sector.
        /// </summary>
        /// <remarks>
        /// We use `long` to avoid arithmetic overflow 
        /// </remarks>
        public long Value { get; }

        public bool IsUnknown() => Value == -1L;

        public SectorIndex(long value) => Value = value;

        public SectorIndex GetNext(int step = 1)
        {
            if (Value + step >= int.MaxValue)
            {
                throw new InvalidOperationException($"Sector index cannot be larger than ${int.MaxValue}");
            }

            return new SectorIndex(Value + step);
        }

        public bool IsWithinRange(SectorIndex maxIndex)
        {
            return Value < maxIndex.Value;
        }

        public long ByteIndex(int sectorSizeInBytes)
        {
            return Value * sectorSizeInBytes;
        }
    }
}