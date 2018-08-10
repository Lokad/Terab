using System;

namespace Terab.UTXO.Core.Hash
{
    /// <summary>
    /// Fast hash - non-crypto secure. Intended to detect data storage
    /// corruption.
    /// </summary>
    /// <remarks>
    /// Based on an original implementation from Adam Horvath's
    /// http://blog.teamleadnet.com/2012/08/murmurhash3-ultra-fast-hash-algorithm.html
    /// </remarks>
    public class MurmurHash3
    {
        // 128 bit output, 64 bit platform version

        private const ulong ReadSize = 16;
        private const ulong C1 = 0x87c37b91114253d5L;
        private const ulong C2 = 0x4cf5ad432745937fL;

        /// <remarks>A random generated default seed.</remarks>
        private const uint DefaultSeed = 0xdbe22f02; 

        private readonly uint _seed;
        

        public MurmurHash3()
        {
            _seed = DefaultSeed;
        }

        public MurmurHash3(uint seed)
        {
            _seed = seed;
        }

        public Hash128 ComputeHash(Span<byte> bb)
        {
            ulong h1 = _seed;
            ulong h2 = 0;
            ulong length = 0;

            int pos = 0;
            ulong remaining = (ulong)bb.Length;

            // read 128 bits, 16 bytes, 2 longs in eacy cycle
            while (remaining >= ReadSize)
            {
                ulong k1 = bb.GetUInt64(pos);
                pos += 8;

                ulong k2 = bb.GetUInt64(pos);
                pos += 8;

                length += ReadSize;
                remaining -= ReadSize;

                MixBody(k1, k2);
            }

            // if the input MOD 16 != 0
            if (remaining > 0)
            {
                ProcessBytesRemaining(bb);
            }

            return CreateHash();

            void MixBody(ulong k1, ulong k2)
            {
                h1 ^= MixKey1(k1);

                h1 = h1.RotateLeft(27);
                h1 += h2;
                h1 = h1 * 5 + 0x52dce729;

                h2 ^= MixKey2(k2);

                h2 = h2.RotateLeft(31);
                h2 += h1;
                h2 = h2 * 5 + 0x38495ab5;
            }

            void ProcessBytesRemaining(Span<byte> rbb)
            {
                ulong k1 = 0;
                ulong k2 = 0;
                length += remaining;

                // little endian (x86) processing
                switch (remaining)
                {
                    case 15:
                        k2 ^= (ulong)rbb[pos + 14] << 48; // fall through
                        goto case 14;
                    case 14:
                        k2 ^= (ulong)rbb[pos + 13] << 40; // fall through
                        goto case 13;
                    case 13:
                        k2 ^= (ulong)rbb[pos + 12] << 32; // fall through
                        goto case 12;
                    case 12:
                        k2 ^= (ulong)rbb[pos + 11] << 24; // fall through
                        goto case 11;
                    case 11:
                        k2 ^= (ulong)rbb[pos + 10] << 16; // fall through
                        goto case 10;
                    case 10:
                        k2 ^= (ulong)rbb[pos + 9] << 8; // fall through
                        goto case 9;
                    case 9:
                        k2 ^= rbb[pos + 8]; // fall through
                        goto case 8;
                    case 8:
                        k1 ^= rbb.GetUInt64(pos);
                        break;
                    case 7:
                        k1 ^= (ulong)rbb[pos + 6] << 48; // fall through
                        goto case 6;
                    case 6:
                        k1 ^= (ulong)rbb[pos + 5] << 40; // fall through
                        goto case 5;
                    case 5:
                        k1 ^= (ulong)rbb[pos + 4] << 32; // fall through
                        goto case 4;
                    case 4:
                        k1 ^= (ulong)rbb[pos + 3] << 24; // fall through
                        goto case 3;
                    case 3:
                        k1 ^= (ulong)rbb[pos + 2] << 16; // fall through
                        goto case 2;
                    case 2:
                        k1 ^= (ulong)rbb[pos + 1] << 8; // fall through
                        goto case 1;
                    case 1:
                        k1 ^= rbb[pos]; // fall through
                        break;
                    default:
                        throw new InvalidOperationException("Something went wrong with remaining bytes calculation.");
                }

                h1 ^= MixKey1(k1);
                h2 ^= MixKey2(k2);
            }

            Hash128 CreateHash()
            {
                h1 ^= length;
                h2 ^= length;

                h1 += h2;
                h2 += h1;

                h1 = MixFinal(h1);
                h2 = MixFinal(h2);

                h1 += h2;
                h2 += h1;

                return new Hash128(h1, h2);
            }
        }

        private static ulong MixKey1(ulong k1)
        {
            k1 *= C1;
            k1 = k1.RotateLeft(31);
            k1 *= C2;
            return k1;
        }

        private static ulong MixKey2(ulong k2)
        {
            k2 *= C2;
            k2 = k2.RotateLeft(33);
            k2 *= C1;
            return k2;
        }

        private static ulong MixFinal(ulong k)
        {
            // avalanche bits

            k ^= k >> 33;
            k *= 0xff51afd7ed558ccdL;
            k ^= k >> 33;
            k *= 0xc4ceb9fe1a85ec53L;
            k ^= k >> 33;
            return k;
        }

        
    }

    static class IntHelpers
    {
        public static ulong RotateLeft(this ulong original, int bits)
        {
            return (original << bits) | (original >> (64 - bits));
        }

        public static ulong RotateRight(this ulong original, int bits)
        {
            return (original >> bits) | (original << (64 - bits));
        }

        public static ulong GetUInt64(this Span<byte> bb, int pos)
        {
            return (ulong)(
                bb[pos++] <<  0 | bb[pos++] <<  8 | bb[pos++] << 16 | bb[pos++] << 24 |
                bb[pos++] << 32 | bb[pos++] << 40 | bb[pos++] << 48 | bb[pos] << 56);
        }

        // [vermorel] 2018-02-13, no Span overload available yet for BitConverter 
        //public static ulong GetUInt64(this byte[] bb, int pos)
        //{
        //    return BitConverter.ToUInt64(bb, pos);
        //}

        // [vermorel] 2018-02-13, we don't want to introduce unsafe code yet
        //unsafe public static ulong GetUInt64(this byte[] bb, int pos)
        //{
        //    // we only read aligned longs, so a simple casting is enough
        //    fixed (byte* pbyte = &bb[pos])
        //    {
        //        return *((ulong*)pbyte);
        //    }
        //}
    }
}
