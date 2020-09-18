




/* 
This is automatically translated code from https://github.com/Blosc/c-blosc

BSD License

For Blosc - A blocking, shuffling and lossless compression library

Copyright (C) 2009-2018 Francesc Alted <francesc@blosc.org>
Copyright (C) 2019-present Blosc Development team <blosc@blosc.org>

Redistribution and use in source and binary forms, with or without modification,
are permitted provided that the following conditions are met:

 * Redistributions of source code must retain the above copyright notice, this
   list of conditions and the following disclaimer.

 * Redistributions in binary form must reproduce the above copyright notice,
   this list of conditions and the following disclaimer in the documentation
   and/or other materials provided with the distribution.

 * Neither the name Francesc Alted nor the names of its contributors may be used
   to endorse or promote products derived from this software without specific
   prior written permission.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE FOR
ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
(INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON
ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 
*/

#if NETCOREAPP3_0

using System;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace HDF5.NET
{
    public static class ShuffleAvx2
    {
        

        /* Routine optimized for shuffling a buffer for a type size of 2 bytes. */
        private static unsafe void shuffle2_avx2(byte* dest, byte* src,
            ulong vectorizable_elements, ulong total_elements)
        {
            ulong bytesoftype = 2;
            ulong j;
            uint k;
            var ymm0 = new Vector256<byte>[2];
            var ymm1 = new Vector256<byte>[2];

            /* Create the shuffle mask.
             NOTE: The XMM/YMM 'set' intrinsics require the arguments to be ordered from
             most to least significant (i.e., their order is reversed when compared to
             loading the mask from an array). */
            var shmask = Vector256.Create((byte)
                0x0f, 0x0d, 0x0b, 0x09, 0x07, 0x05, 0x03, 0x01,
                0x0e, 0x0c, 0x0a, 0x08, 0x06, 0x04, 0x02, 0x00,
                0x0f, 0x0d, 0x0b, 0x09, 0x07, 0x05, 0x03, 0x01,
                0x0e, 0x0c, 0x0a, 0x08, 0x06, 0x04, 0x02, 0x00);

            for (j = 0; j < vectorizable_elements; j += (ulong)sizeof(Vector256<byte>)) {
                /* Fetch 32 elements (64 bytes) then transpose bytes, words and double words. */
                for (k = 0; k < 2; k++) {
                    ymm0[k] = Avx.LoadVector256((src + (j * bytesoftype) + (k * (ulong)sizeof(Vector256<byte>))));
                    ymm1[k] = Avx2.Shuffle(ymm0[k], shmask);
                }

                ymm0[0] = Avx2.Permute4x64(ymm1[0].AsInt64(), 0xd8).AsByte();
                ymm0[1] = Avx2.Permute4x64(ymm1[1].AsInt64(), 0x8d).AsByte();

                ymm1[0] = Avx2.Blend(ymm0[0].AsInt32(), ymm0[1].AsInt32(), 0xf0).AsByte();
                ymm0[1] = Avx2.Blend(ymm0[0].AsInt32(), ymm0[1].AsInt32(), 0x0f).AsByte();
                ymm1[1] = Avx2.Permute4x64(ymm0[1].AsInt64(), 0x4e).AsByte();

                /* Store the result vectors */
                byte* dest_for_jth_element = dest + j;
                for (k = 0; k < 2; k++) {
                    Avx2.Store((dest_for_jth_element + (k * total_elements)), ymm1[k]);
                }
            }
        }

        /* Routine optimized for shuffling a buffer for a type size of 4 bytes. */
        private static unsafe void shuffle4_avx2(byte* dest, byte* src,
            ulong vectorizable_elements, ulong total_elements)
        {
            ulong bytesoftype = 4;
            ulong i;
            uint j;
            var ymm0 = new Vector256<byte>[4];
            var ymm1 = new Vector256<byte>[4];

            /* Create the shuffle mask.
             NOTE: The XMM/YMM 'set' intrinsics require the arguments to be ordered from
             most to least significant (i.e., their order is reversed when compared to
             loading the mask from an array). */
            var mask = Vector256.Create((int)
                0x07, 0x03, 0x06, 0x02, 0x05, 0x01, 0x04, 0x00);

            for (i = 0; i < vectorizable_elements; i += (ulong)sizeof(Vector256<byte>)) {
                /* Fetch 32 elements (128 bytes) then transpose bytes and words. */
                for (j = 0; j < 4; j++) {
                    ymm0[j] = Avx.LoadVector256((src + (i * bytesoftype) + (j * (ulong)sizeof(Vector256<byte>))));
                    ymm1[j] = Avx2.Shuffle(ymm0[j].AsInt32(), 0xd8).AsByte();
                    ymm0[j] = Avx2.Shuffle(ymm0[j].AsInt32(), 0x8d).AsByte();
                    ymm0[j] = Avx2.UnpackLow(ymm1[j], ymm0[j]);
                    ymm1[j] = Avx2.Shuffle(ymm0[j].AsInt32(), 0x04e).AsByte();
                    ymm0[j] = Avx2.UnpackLow(ymm0[j], ymm1[j]);
                }
                /* Transpose double words */
                for (j = 0; j < 2; j++) {
                    ymm1[j*2] = Avx2.UnpackLow(ymm0[j*2], ymm0[j*2+1]);
                    ymm1[j*2+1] = Avx2.UnpackHigh(ymm0[j*2], ymm0[j*2+1]);
                }
                /* Transpose quad words */
                for (j = 0; j < 2; j++) {
                    ymm0[j*2] = Avx2.UnpackLow(ymm1[j], ymm1[j+2]);
                    ymm0[j*2+1] = Avx2.UnpackHigh(ymm1[j], ymm1[j+2]);
                }
                for (j = 0; j < 4; j++) {
                    ymm0[j] = Avx2.PermuteVar8x32(ymm0[j].AsInt32(), mask).AsByte();
                }
                /* Store the result vectors */
                byte* dest_for_ith_element = dest + i;
                for (j = 0; j < 4; j++) {
                    Avx2.Store((dest_for_ith_element + (j * total_elements)), ymm0[j]);
                }
            }
        }

        /* Routine optimized for shuffling a buffer for a type size of 8 bytes. */
        private static unsafe void shuffle8_avx2(byte* dest, byte* src,
            ulong vectorizable_elements, ulong total_elements)
        {
            ulong bytesoftype = 8;
            ulong j;
            uint k; int l;
            var ymm0 = new Vector256<byte>[8];
            var ymm1 = new Vector256<byte>[8];

            for (j = 0; j < vectorizable_elements; j += (ulong)sizeof(Vector256<byte>)) {
                /* Fetch 32 elements (256 bytes) then transpose bytes. */
                for (k = 0; k < 8; k++) {
                    ymm0[k] = Avx.LoadVector256((src + (j * bytesoftype) + (k * (ulong)sizeof(Vector256<byte>))));
                    ymm1[k] = Avx2.Shuffle(ymm0[k].AsInt32(), 0x4e).AsByte();
                    ymm1[k] = Avx2.UnpackLow(ymm0[k], ymm1[k]);
                }
                /* Transpose words */
                for (k = 0, l = 0; k < 4; k++, l +=2) {
                    ymm0[k*2] = Avx2.UnpackLow(ymm1[l], ymm1[l+1]);
                    ymm0[k*2+1] = Avx2.UnpackHigh(ymm1[l], ymm1[l+1]);
                }
                /* Transpose double words */
                for (k = 0, l = 0; k < 4; k++, l++) {
                    if (k == 2) l += 2;
                    ymm1[k*2] = Avx2.UnpackLow(ymm0[l], ymm0[l+2]);
                    ymm1[k*2+1] = Avx2.UnpackHigh(ymm0[l], ymm0[l+2]);
                }
                /* Transpose quad words */
                for (k = 0; k < 4; k++) {
                    ymm0[k*2] = Avx2.UnpackLow(ymm1[k], ymm1[k+4]);
                    ymm0[k*2+1] = Avx2.UnpackHigh(ymm1[k], ymm1[k+4]);
                }
                for(k = 0; k < 8; k++) {
                    ymm1[k] = Avx2.Permute4x64(ymm0[k].AsInt64(), 0x72).AsByte();
                    ymm0[k] = Avx2.Permute4x64(ymm0[k].AsInt64(), 0xD8).AsByte();
                    ymm0[k] = Avx2.UnpackLow(ymm0[k], ymm1[k]);
                }
                /* Store the result vectors */
                byte* dest_for_jth_element = dest + j;
                for (k = 0; k < 8; k++) {
                    Avx2.Store((dest_for_jth_element + (k * total_elements)), ymm0[k]);
                }
            }
        }

        /* Routine optimized for shuffling a buffer for a type size of 16 bytes. */
        private static unsafe void shuffle16_avx2(byte* dest, byte* src,
            ulong vectorizable_elements, ulong total_elements)
        {
            ulong bytesoftype = 16;
            ulong j;
            uint k; int l;
            var ymm0 = new Vector256<byte>[16];
            var ymm1 = new Vector256<byte>[16];

            /* Create the shuffle mask.
             NOTE: The XMM/YMM 'set' intrinsics require the arguments to be ordered from
             most to least significant (i.e., their order is reversed when compared to
             loading the mask from an array). */
            var shmask = Vector256.Create((byte)
                0x0f, 0x07, 0x0e, 0x06, 0x0d, 0x05, 0x0c, 0x04,
                0x0b, 0x03, 0x0a, 0x02, 0x09, 0x01, 0x08, 0x00,
                0x0f, 0x07, 0x0e, 0x06, 0x0d, 0x05, 0x0c, 0x04,
                0x0b, 0x03, 0x0a, 0x02, 0x09, 0x01, 0x08, 0x00);

            for (j = 0; j < vectorizable_elements; j += (ulong)sizeof(Vector256<byte>)) {
                /* Fetch 32 elements (512 bytes) into 16 YMM registers. */
                for (k = 0; k < 16; k++) {
                    ymm0[k] = Avx.LoadVector256((src + (j * bytesoftype) + (k * (ulong)sizeof(Vector256<byte>))));
                }
                /* Transpose bytes */
                for (k = 0, l = 0; k < 8; k++, l +=2) {
                    ymm1[k*2] = Avx2.UnpackLow(ymm0[l], ymm0[l+1]);
                    ymm1[k*2+1] = Avx2.UnpackHigh(ymm0[l], ymm0[l+1]);
                }
                /* Transpose words */
                for (k = 0, l = -2; k < 8; k++, l++) {
                    if ((k%2) == 0) l += 2;
                    ymm0[k*2] = Avx2.UnpackLow(ymm1[l], ymm1[l+2]);
                    ymm0[k*2+1] = Avx2.UnpackHigh(ymm1[l], ymm1[l+2]);
                }
                /* Transpose double words */
                for (k = 0, l = -4; k < 8; k++, l++) {
                    if ((k%4) == 0) l += 4;
                    ymm1[k*2] = Avx2.UnpackLow(ymm0[l], ymm0[l+4]);
                    ymm1[k*2+1] = Avx2.UnpackHigh(ymm0[l], ymm0[l+4]);
                }
                /* Transpose quad words */
                for (k = 0; k < 8; k++) {
                    ymm0[k*2] = Avx2.UnpackLow(ymm1[k], ymm1[k+8]);
                    ymm0[k*2+1] = Avx2.UnpackHigh(ymm1[k], ymm1[k+8]);
                }
                for (k = 0; k < 16; k++) {
                    ymm0[k] = Avx2.Permute4x64(ymm0[k].AsInt64(), 0xd8).AsByte();
                    ymm0[k] = Avx2.Shuffle(ymm0[k], shmask);
                }
                /* Store the result vectors */
                byte* dest_for_jth_element = dest + j;
                for (k = 0; k < 16; k++) {
                    Avx2.Store((dest_for_jth_element + (k * total_elements)), ymm0[k]);
                }
            }
        }

        /* Routine optimized for unshuffling a buffer for a type size of 2 bytes. */
        private static unsafe void unshuffle2_avx2(byte* dest, byte* src,
            ulong vectorizable_elements, ulong total_elements)
        {
            ulong bytesoftype = 2;
            ulong i;
            uint j;
            var ymm0 = new Vector256<byte>[2];
            var ymm1 = new Vector256<byte>[2];

            for (i = 0; i < vectorizable_elements; i += (ulong)sizeof(Vector256<byte>)) {
                /* Load 32 elements (64 bytes) into 2 YMM registers. */
                byte* src_for_ith_element = src + i;
                for (j = 0; j < 2; j++) {
                    ymm0[j] = Avx.LoadVector256((src_for_ith_element + (j * total_elements)));
                }
                /* Shuffle bytes */
                for (j = 0; j < 2; j++) {
                    ymm0[j] = Avx2.Permute4x64(ymm0[j].AsInt64(), 0xd8).AsByte();
                }
                /* Compute the low 64 bytes */
                ymm1[0] = Avx2.UnpackLow(ymm0[0], ymm0[1]);
                /* Compute the hi 64 bytes */
                ymm1[1] = Avx2.UnpackHigh(ymm0[0], ymm0[1]);
                /* Store the result vectors in proper order */
                Avx2.Store((dest + (i * bytesoftype) + (0 * (ulong)sizeof(Vector256<byte>))), ymm1[0]);
                Avx2.Store((dest + (i * bytesoftype) + (1 * (ulong)sizeof(Vector256<byte>))), ymm1[1]);
            }
        }

        /* Routine optimized for unshuffling a buffer for a type size of 4 bytes. */
        private static unsafe void unshuffle4_avx2(byte* dest, byte* src,
            ulong vectorizable_elements, ulong total_elements)
        {
            ulong bytesoftype = 4;
            ulong i;
            uint j;
            var ymm0 = new Vector256<byte>[4];
            var ymm1 = new Vector256<byte>[4];

            for (i = 0; i < vectorizable_elements; i += (ulong)sizeof(Vector256<byte>)) {
                /* Load 32 elements (128 bytes) into 4 YMM registers. */
                byte* src_for_ith_element = src + i;
                for (j = 0; j < 4; j++) {
                    ymm0[j] = Avx.LoadVector256((src_for_ith_element + (j * total_elements)));
                }
                /* Shuffle bytes */
                for (j = 0; j < 2; j++) {
                    /* Compute the low 64 bytes */
                    ymm1[j] = Avx2.UnpackLow(ymm0[j*2], ymm0[j*2+1]);
                    /* Compute the hi 64 bytes */
                    ymm1[2+j] = Avx2.UnpackHigh(ymm0[j*2], ymm0[j*2+1]);
                }
                /* Shuffle 2-byte words */
                for (j = 0; j < 2; j++) {
                    /* Compute the low 64 bytes */
                    ymm0[j] = Avx2.UnpackLow(ymm1[j*2], ymm1[j*2+1]);
                    /* Compute the hi 64 bytes */
                    ymm0[2+j] = Avx2.UnpackHigh(ymm1[j*2], ymm1[j*2+1]);
                }
                ymm1[0] = Avx2.Permute2x128(ymm0[0], ymm0[2], 0x20);
                ymm1[1] = Avx2.Permute2x128(ymm0[1], ymm0[3], 0x20);
                ymm1[2] = Avx2.Permute2x128(ymm0[0], ymm0[2], 0x31);
                ymm1[3] = Avx2.Permute2x128(ymm0[1], ymm0[3], 0x31);

                /* Store the result vectors in proper order */
                for (j = 0; j < 4; j++) {
                    Avx2.Store((dest + (i * bytesoftype) + (j * (ulong)sizeof(Vector256<byte>))), ymm1[j]);
                }
            }
        }

        /* Routine optimized for unshuffling a buffer for a type size of 8 bytes. */
        private static unsafe void unshuffle8_avx2(byte* dest, byte* src,
            ulong vectorizable_elements, ulong total_elements)
        {
            ulong bytesoftype = 8;
            ulong i;
            uint j;
            var ymm0 = new Vector256<byte>[8];
            var ymm1 = new Vector256<byte>[8];

            for (i = 0; i < vectorizable_elements; i += (ulong)sizeof(Vector256<byte>)) {
                /* Fetch 32 elements (256 bytes) into 8 YMM registers. */
                byte* src_for_ith_element = src + i;
                for (j = 0; j < 8; j++) {
                    ymm0[j] = Avx.LoadVector256((src_for_ith_element + (j * total_elements)));
                }
                /* Shuffle bytes */
                for (j = 0; j < 4; j++) {
                    /* Compute the low 32 bytes */
                    ymm1[j] = Avx2.UnpackLow(ymm0[j*2], ymm0[j*2+1]);
                    /* Compute the hi 32 bytes */
                    ymm1[4+j] = Avx2.UnpackHigh(ymm0[j*2], ymm0[j*2+1]);
                }
                /* Shuffle words */
                for (j = 0; j < 4; j++) {
                    /* Compute the low 32 bytes */
                    ymm0[j] = Avx2.UnpackLow(ymm1[j*2], ymm1[j*2+1]);
                    /* Compute the hi 32 bytes */
                    ymm0[4+j] = Avx2.UnpackHigh(ymm1[j*2], ymm1[j*2+1]);
                }
                for (j = 0; j < 8; j++) {
                    ymm0[j] = Avx2.Permute4x64(ymm0[j].AsInt64(), 0xd8).AsByte();
                }

                /* Shuffle 4-byte dwords */
                for (j = 0; j < 4; j++) {
                    /* Compute the low 32 bytes */
                    ymm1[j] = Avx2.UnpackLow(ymm0[j*2], ymm0[j*2+1]);
                    /* Compute the hi 32 bytes */
                    ymm1[4+j] = Avx2.UnpackHigh(ymm0[j*2], ymm0[j*2+1]);
                }

                /* Store the result vectors in proper order */
                Avx2.Store((dest + (i * bytesoftype) + (0 * (ulong)sizeof(Vector256<byte>))), ymm1[0]);
                Avx2.Store((dest + (i * bytesoftype) + (1 * (ulong)sizeof(Vector256<byte>))), ymm1[2]);
                Avx2.Store((dest + (i * bytesoftype) + (2 * (ulong)sizeof(Vector256<byte>))), ymm1[1]);
                Avx2.Store((dest + (i * bytesoftype) + (3 * (ulong)sizeof(Vector256<byte>))), ymm1[3]);
                Avx2.Store((dest + (i * bytesoftype) + (4 * (ulong)sizeof(Vector256<byte>))), ymm1[4]);
                Avx2.Store((dest + (i * bytesoftype) + (5 * (ulong)sizeof(Vector256<byte>))), ymm1[6]);
                Avx2.Store((dest + (i * bytesoftype) + (6 * (ulong)sizeof(Vector256<byte>))), ymm1[5]);
                Avx2.Store((dest + (i * bytesoftype) + (7 * (ulong)sizeof(Vector256<byte>))), ymm1[7]);
            }
        }

        /* Routine optimized for unshuffling a buffer for a type size of 16 bytes. */
        private static unsafe void unshuffle16_avx2(byte* dest, byte* src,
            ulong vectorizable_elements, ulong total_elements)
        {
            ulong bytesoftype = 16;
            ulong i;
            uint j;
            var ymm0 = new Vector256<byte>[16];
            var ymm1 = new Vector256<byte>[16];

            for (i = 0; i < vectorizable_elements; i += (ulong)sizeof(Vector256<byte>)) {
                /* Fetch 32 elements (512 bytes) into 16 YMM registers. */
                byte* src_for_ith_element = src + i;
                for (j = 0; j < 16; j++) {
                    ymm0[j] = Avx.LoadVector256((src_for_ith_element + (j * total_elements)));
                }

                /* Shuffle bytes */
                for (j = 0; j < 8; j++) {
                    /* Compute the low 32 bytes */
                    ymm1[j] = Avx2.UnpackLow(ymm0[j*2], ymm0[j*2+1]);
                    /* Compute the hi 32 bytes */
                    ymm1[8+j] = Avx2.UnpackHigh(ymm0[j*2], ymm0[j*2+1]);
                }
                /* Shuffle 2-byte words */
                for (j = 0; j < 8; j++) {
                    /* Compute the low 32 bytes */
                    ymm0[j] = Avx2.UnpackLow(ymm1[j*2], ymm1[j*2+1]);
                    /* Compute the hi 32 bytes */
                    ymm0[8+j] = Avx2.UnpackHigh(ymm1[j*2], ymm1[j*2+1]);
                }
                /* Shuffle 4-byte dwords */
                for (j = 0; j < 8; j++) {
                    /* Compute the low 32 bytes */
                    ymm1[j] = Avx2.UnpackLow(ymm0[j*2], ymm0[j*2+1]);
                    /* Compute the hi 32 bytes */
                    ymm1[8+j] = Avx2.UnpackHigh(ymm0[j*2], ymm0[j*2+1]);
                }

                /* Shuffle 8-byte qwords */
                for (j = 0; j < 8; j++) {
                    /* Compute the low 32 bytes */
                    ymm0[j] = Avx2.UnpackLow(ymm1[j*2], ymm1[j*2+1]);
                    /* Compute the hi 32 bytes */
                    ymm0[8+j] = Avx2.UnpackHigh(ymm1[j*2], ymm1[j*2+1]);
                }

                for (j = 0; j < 8; j++) {
                    ymm1[j] = Avx2.Permute2x128(ymm0[j], ymm0[j+8], 0x20);
                    ymm1[j+8] = Avx2.Permute2x128(ymm0[j], ymm0[j+8], 0x31);
                }

                /* Store the result vectors in proper order */
                Avx2.Store((dest + (i * bytesoftype) + (0 * (ulong)sizeof(Vector256<byte>))), ymm1[0]);
                Avx2.Store((dest + (i * bytesoftype) + (1 * (ulong)sizeof(Vector256<byte>))), ymm1[4]);
                Avx2.Store((dest + (i * bytesoftype) + (2 * (ulong)sizeof(Vector256<byte>))), ymm1[2]);
                Avx2.Store((dest + (i * bytesoftype) + (3 * (ulong)sizeof(Vector256<byte>))), ymm1[6]);
                Avx2.Store((dest + (i * bytesoftype) + (4 * (ulong)sizeof(Vector256<byte>))), ymm1[1]);
                Avx2.Store((dest + (i * bytesoftype) + (5 * (ulong)sizeof(Vector256<byte>))), ymm1[5]);
                Avx2.Store((dest + (i * bytesoftype) + (6 * (ulong)sizeof(Vector256<byte>))), ymm1[3]);
                Avx2.Store((dest + (i * bytesoftype) + (7 * (ulong)sizeof(Vector256<byte>))), ymm1[7]);
                Avx2.Store((dest + (i * bytesoftype) + (8 * (ulong)sizeof(Vector256<byte>))), ymm1[8]);
                Avx2.Store((dest + (i * bytesoftype) + (9 * (ulong)sizeof(Vector256<byte>))), ymm1[12]);
                Avx2.Store((dest + (i * bytesoftype) + (10 * (ulong)sizeof(Vector256<byte>))), ymm1[10]);
                Avx2.Store((dest + (i * bytesoftype) + (11 * (ulong)sizeof(Vector256<byte>))), ymm1[14]);
                Avx2.Store((dest + (i * bytesoftype) + (12 * (ulong)sizeof(Vector256<byte>))), ymm1[9]);
                Avx2.Store((dest + (i * bytesoftype) + (13 * (ulong)sizeof(Vector256<byte>))), ymm1[13]);
                Avx2.Store((dest + (i * bytesoftype) + (14 * (ulong)sizeof(Vector256<byte>))), ymm1[11]);
                Avx2.Store((dest + (i * bytesoftype) + (15 * (ulong)sizeof(Vector256<byte>))), ymm1[15]);
            }
        }

        /* Shuffle a block.  This can never fail. */
        private static unsafe void blosc_internal_shuffle_avx2(ulong bytesoftype, ulong blocksize,
                            byte* _src, byte* _dest) {
            ulong vectorized_chunk_size = bytesoftype * (ulong)sizeof(Vector256<byte>);

            /* If the block size is too small to be vectorized,
             use the generic implementation. */
            if (blocksize < vectorized_chunk_size) {
                ShuffleGeneric.Shuffle(bytesoftype, 0, blocksize, _src, _dest);
                return;
            }

            /* If the blocksize is not a multiple of both the typesize and
             the vector size, round the blocksize down to the next value
             which is a multiple of both. The vectorized shuffle can be
             used for that portion of the data, and the naive implementation
             can be used for the remaining portion. */
            ulong vectorizable_bytes = blocksize - (blocksize % vectorized_chunk_size);

            ulong vectorizable_elements = vectorizable_bytes / bytesoftype;
            ulong total_elements = blocksize / bytesoftype;

            /* Optimized shuffle implementations */
            switch (bytesoftype)
            {
            case 2:
                shuffle2_avx2(_dest, _src, vectorizable_elements, total_elements);
                break;
            case 4:
                shuffle4_avx2(_dest, _src, vectorizable_elements, total_elements);
                break;
            case 8:
                shuffle8_avx2(_dest, _src, vectorizable_elements, total_elements);
                break;
            case 16:
                shuffle16_avx2(_dest, _src, vectorizable_elements, total_elements);
                break;
            throw new Exception("No optimized AVX Implementation found.");
            }

            /* If the buffer had any bytes at the end which couldn't be handled
             by the vectorized implementations, use the non-optimized version
             to finish them up. */
            if (vectorizable_bytes < blocksize) {
                ShuffleGeneric.Shuffle(bytesoftype, vectorizable_bytes, blocksize, _src, _dest);
            }
        }

        /* Unshuffle a block.  This can never fail. */
        private static unsafe void blosc_internal_unshuffle_avx2(ulong bytesoftype, ulong blocksize,
                              byte* _src, byte* _dest) {
            ulong vectorized_chunk_size = bytesoftype * (ulong)sizeof(Vector256<byte>);

            /* If the block size is too small to be vectorized,
             use the generic implementation. */
            if (blocksize < vectorized_chunk_size) {
                ShuffleGeneric.Unshuffle(bytesoftype, 0, blocksize, _src, _dest);
                return;
            }

            /* If the blocksize is not a multiple of both the typesize and
             the vector size, round the blocksize down to the next value
             which is a multiple of both. The vectorized unshuffle can be
             used for that portion of the data, and the naive implementation
             can be used for the remaining portion. */
            ulong vectorizable_bytes = blocksize - (blocksize % vectorized_chunk_size);

            ulong vectorizable_elements = vectorizable_bytes / bytesoftype;
            ulong total_elements = blocksize / bytesoftype;

            /* Optimized unshuffle implementations */
            switch (bytesoftype)
            {
            case 2:
                unshuffle2_avx2(_dest, _src, vectorizable_elements, total_elements);
                break;
            case 4:
                unshuffle4_avx2(_dest, _src, vectorizable_elements, total_elements);
                break;
            case 8:
                unshuffle8_avx2(_dest, _src, vectorizable_elements, total_elements);
                break;
            case 16:
                unshuffle16_avx2(_dest, _src, vectorizable_elements, total_elements);
                break;
            throw new Exception("No optimized AVX Implementation found.");
            }

            /* If the buffer had any bytes at the end which couldn't be handled
             by the vectorized implementations, use the non-optimized version
             to finish them up. */
            if (vectorizable_bytes < blocksize) {
                ShuffleGeneric.Unshuffle(bytesoftype, vectorizable_bytes, blocksize, _src, _dest);
            }
        }


    }
}

#endif