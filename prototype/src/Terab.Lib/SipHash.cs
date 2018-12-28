// Downloaded from: https://github.com/paya-cz/siphash
// Author:          Pavel Werl
// License:         Public Domain
// SipHash website: https://131002.net/siphash/

using System;
using System.Runtime.InteropServices;
using Terab.Lib.Coins;
using Terab.Lib.Messaging;

namespace Terab.Lib
{
    /// <summary>
    /// This class is immutable and thread-safe.
    /// </summary>
    public sealed class SipHash : IOutpointHash
    {
        /// <summary>
        /// Part of the initial 256-bit internal state.
        /// </summary>
        private readonly ulong _initialState0;
        /// <summary>
        /// Part of the initial 256-bit internal state.
        /// </summary>
        private readonly ulong _initialState1;

        /// <summary>
        /// Initializes a new instance of SipHash pseudo-random function using the specified 128-bit key.
        /// </summary>
        /// <param name="key">
        /// <para>Key for the SipHash pseudo-random function.</para>
        /// <para>Must be exactly 16 bytes long and must not be null.</para>
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="key"/> is null.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="key"/> is not exactly 16 bytes long (128 bits).
        /// </exception>
        public SipHash(ReadOnlySpan<byte> key)
        {
            if (key.IsEmpty)
                throw new ArgumentNullException(nameof(key));

            if (key.Length != 16)
            {
                throw new ArgumentException("SipHash key must be exactly 128-bit long (16 bytes).", nameof(key));
            }

            var keyAsUInt64Array = MemoryMarshal.Cast<byte, ulong>(key);

            _initialState0 = 0x736f6d6570736575UL ^ keyAsUInt64Array[0];
            _initialState1 = 0x646f72616e646f6dUL ^ keyAsUInt64Array[1];
        }

        public ulong Hash(ref Outpoint outpoint)
        {
            return this.Compute(MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(ref outpoint, 1)));
        }

        /// <summary>
        /// Gets a 128-bit SipHash key.
        /// </summary>
        public Span<byte> Key
        {
            get
            {
                var key = new ulong[2];
                key[0] = _initialState0 ^ 0x736f6d6570736575UL;
                key[1] = _initialState1 ^ 0x646f72616e646f6dUL;

                return MemoryMarshal.Cast<ulong, byte>(key);
            }
        }

        /// <summary>
        /// Computes 64-bit SipHash tag for the specified message.
        /// </summary>
        /// <param name="data">
        /// <para>The span byte for which to computer SipHash tag.</para>
        /// <para>Must not be null.</para>
        /// </param>
        /// <returns>Returns 64-bit (8 bytes) SipHash tag.</returns>
        public ulong Compute(ReadOnlySpan<byte> data)
        {
            int count = data.Length;
            // SipHash internal state
            var v0 = _initialState0;
            var v1 = _initialState1;
            // It is faster to load the initialStateX fields from memory again than to reference v0 and v1:
            var v2 = 0x1F160A001E161714UL ^ _initialState0;
            var v3 = 0x100A160317100A1EUL ^ _initialState1;

            // We process data in 64-bit blocks
            ulong block;

            unsafe
            {
                // Start of the data to process
                fixed (byte* dataStart = &data.GetPinnableReference())
                {
                    // The last 64-bit block of data
                    var finalBlock = dataStart + (count & ~7);

                    // Process the input data in blocks of 64 bits
                    for (var blockPointer = (ulong*)dataStart; blockPointer < finalBlock;)
                    {
                        block = *blockPointer++;

                        v3 ^= block;

                        // Round 1
                        v0 += v1;
                        v2 += v3;
                        v1 = v1 << 13 | v1 >> 51;
                        v3 = v3 << 16 | v3 >> 48;
                        v1 ^= v0;
                        v3 ^= v2;
                        v0 = v0 << 32 | v0 >> 32;
                        v2 += v1;
                        v0 += v3;
                        v1 = v1 << 17 | v1 >> 47;
                        v3 = v3 << 21 | v3 >> 43;
                        v1 ^= v2;
                        v3 ^= v0;
                        v2 = v2 << 32 | v2 >> 32;

                        // Round 2
                        v0 += v1;
                        v2 += v3;
                        v1 = v1 << 13 | v1 >> 51;
                        v3 = v3 << 16 | v3 >> 48;
                        v1 ^= v0;
                        v3 ^= v2;
                        v0 = v0 << 32 | v0 >> 32;
                        v2 += v1;
                        v0 += v3;
                        v1 = v1 << 17 | v1 >> 47;
                        v3 = v3 << 21 | v3 >> 43;
                        v1 ^= v2;
                        v3 ^= v0;
                        v2 = v2 << 32 | v2 >> 32;

                        v0 ^= block;
                    }

                    // Load the remaining bytes
                    block = (ulong)count << 56;
                    switch (count & 7)
                    {
                        case 7:
                            block |= *(uint*)finalBlock | (ulong)*(ushort*)(finalBlock + 4) << 32 | (ulong)*(finalBlock + 6) << 48;
                            break;
                        case 6:
                            block |= *(uint*)finalBlock | (ulong)*(ushort*)(finalBlock + 4) << 32;
                            break;
                        case 5:
                            block |= *(uint*)finalBlock | (ulong)*(finalBlock + 4) << 32;
                            break;
                        case 4:
                            block |= *(uint*)finalBlock;
                            break;
                        case 3:
                            block |= *(ushort*)finalBlock | (ulong)*(finalBlock + 2) << 16;
                            break;
                        case 2:
                            block |= *(ushort*)finalBlock;
                            break;
                        case 1:
                            block |= *finalBlock;
                            break;
                    }
                }
            }

            // Process the final block
            {
                v3 ^= block;

                // Round 1
                v0 += v1;
                v2 += v3;
                v1 = v1 << 13 | v1 >> 51;
                v3 = v3 << 16 | v3 >> 48;
                v1 ^= v0;
                v3 ^= v2;
                v0 = v0 << 32 | v0 >> 32;
                v2 += v1;
                v0 += v3;
                v1 = v1 << 17 | v1 >> 47;
                v3 = v3 << 21 | v3 >> 43;
                v1 ^= v2;
                v3 ^= v0;
                v2 = v2 << 32 | v2 >> 32;

                // Round 2
                v0 += v1;
                v2 += v3;
                v1 = v1 << 13 | v1 >> 51;
                v3 = v3 << 16 | v3 >> 48;
                v1 ^= v0;
                v3 ^= v2;
                v0 = v0 << 32 | v0 >> 32;
                v2 += v1;
                v0 += v3;
                v1 = v1 << 17 | v1 >> 47;
                v3 = v3 << 21 | v3 >> 43;
                v1 ^= v2;
                v3 ^= v0;
                v2 = v2 << 32 | v2 >> 32;

                v0 ^= block;
                v2 ^= 0xff;
            }

            // 4 finalization rounds
            {
                // Round 1
                v0 += v1;
                v2 += v3;
                v1 = v1 << 13 | v1 >> 51;
                v3 = v3 << 16 | v3 >> 48;
                v1 ^= v0;
                v3 ^= v2;
                v0 = v0 << 32 | v0 >> 32;
                v2 += v1;
                v0 += v3;
                v1 = v1 << 17 | v1 >> 47;
                v3 = v3 << 21 | v3 >> 43;
                v1 ^= v2;
                v3 ^= v0;
                v2 = v2 << 32 | v2 >> 32;

                // Round 2
                v0 += v1;
                v2 += v3;
                v1 = v1 << 13 | v1 >> 51;
                v3 = v3 << 16 | v3 >> 48;
                v1 ^= v0;
                v3 ^= v2;
                v0 = v0 << 32 | v0 >> 32;
                v2 += v1;
                v0 += v3;
                v1 = v1 << 17 | v1 >> 47;
                v3 = v3 << 21 | v3 >> 43;
                v1 ^= v2;
                v3 ^= v0;
                v2 = v2 << 32 | v2 >> 32;

                // Round 3
                v0 += v1;
                v2 += v3;
                v1 = v1 << 13 | v1 >> 51;
                v3 = v3 << 16 | v3 >> 48;
                v1 ^= v0;
                v3 ^= v2;
                v0 = v0 << 32 | v0 >> 32;
                v2 += v1;
                v0 += v3;
                v1 = v1 << 17 | v1 >> 47;
                v3 = v3 << 21 | v3 >> 43;
                v1 ^= v2;
                v3 ^= v0;
                v2 = v2 << 32 | v2 >> 32;

                // Round 4
                v0 += v1;
                v2 += v3;
                v1 = v1 << 13 | v1 >> 51;
                v3 = v3 << 16 | v3 >> 48;
                v1 ^= v0;
                v3 ^= v2;
                v0 = v0 << 32 | v0 >> 32;
                v2 += v1;
                v0 += v3;
                v1 = v1 << 17 | v1 >> 47;
                v3 = v3 << 21 | v3 >> 43;
                v1 ^= v2;
                v3 ^= v0;
                v2 = v2 << 32 | v2 >> 32;
            }

            return (v0 ^ v1) ^ (v2 ^ v3);
        }
    }
}
