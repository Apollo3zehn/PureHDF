/* 
This is automatically translated code from https://github.com/Blosc/c-blosc2

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

#if NET6_0_OR_GREATER

using System;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace PureHDF.Filters;

internal static class ShuffleSse2
{
    public static unsafe void DoShuffle(int bytesOfType, Span<byte> source, Span<byte> destination)
    {
        fixed (byte* src = source, dest = destination)
        {
            ShuffleSse2.shuffle_sse2(bytesOfType, source.Length, src, dest);
        }
    }

    public static unsafe void DoUnshuffle(int bytesOfType, Span<byte> source, Span<byte> destination)
    {
        fixed (byte* src = source, dest = destination)
        {
            ShuffleSse2.unshuffle_sse2(bytesOfType, source.Length, src, dest);
        }
    }



    /* Routine optimized for shuffling a buffer for a type size of 2 bytes. */
    private static unsafe void shuffle2_sse2(byte* dest, byte* src,
                    int vectorizable_elements, int total_elements)
    {
        int bytesoftype = 2;
        int j;
        int k;
        byte* dest_for_jth_element;
        var xmm0 = new Vector128<byte>[2];
        var xmm1 = new Vector128<byte>[2];

        for (j = 0; j < vectorizable_elements; j += sizeof(Vector128<byte>))
        {
            /* Fetch 16 elements (32 bytes) then transpose bytes, words and double words. */
            for (k = 0; k < 2; k++)
            {
                xmm0[k] = Sse2.LoadVector128((byte*)(src + (j * bytesoftype) + (k * sizeof(Vector128<byte>))));
                xmm0[k] = Sse2.ShuffleLow(xmm0[k].AsInt16(), 0xd8).AsByte();
                xmm0[k] = Sse2.ShuffleHigh(xmm0[k].AsInt16(), 0xd8).AsByte();
                xmm0[k] = Sse2.Shuffle(xmm0[k].AsInt32(), 0xd8).AsByte();
                xmm1[k] = Sse2.Shuffle(xmm0[k].AsInt32(), 0x4e).AsByte();
                xmm0[k] = Sse2.UnpackLow(xmm0[k], xmm1[k]);
                xmm0[k] = Sse2.Shuffle(xmm0[k].AsInt32(), 0xd8).AsByte();
                xmm1[k] = Sse2.Shuffle(xmm0[k].AsInt32(), 0x4e).AsByte();
                xmm0[k] = Sse2.UnpackLow(xmm0[k].AsInt16(), xmm1[k].AsInt16()).AsByte();
                xmm0[k] = Sse2.Shuffle(xmm0[k].AsInt32(), 0xd8).AsByte();
            }
            /* Transpose quad words */
            for (k = 0; k < 1; k++)
            {
                xmm1[k * 2] = Sse2.UnpackLow(xmm0[k].AsInt64(), xmm0[k + 1].AsInt64()).AsByte();
                xmm1[k * 2 + 1] = Sse2.UnpackHigh(xmm0[k].AsInt64(), xmm0[k + 1].AsInt64()).AsByte();
            }
            /* Store the result vectors */
            dest_for_jth_element = dest + j;
            for (k = 0; k < 2; k++)
            {
                Sse2.Store((byte*)(dest_for_jth_element + (k * total_elements)), xmm1[k]);
            }
        }
    }

    /* Routine optimized for shuffling a buffer for a type size of 4 bytes. */
    private static unsafe void shuffle4_sse2(byte* dest, byte* src,
                    int vectorizable_elements, int total_elements)
    {
        int bytesoftype = 4;
        int i;
        int j;
        byte* dest_for_ith_element;
        var xmm0 = new Vector128<byte>[4];
        var xmm1 = new Vector128<byte>[4];

        for (i = 0; i < vectorizable_elements; i += sizeof(Vector128<byte>))
        {
            /* Fetch 16 elements (64 bytes) then transpose bytes and words. */
            for (j = 0; j < 4; j++)
            {
                xmm0[j] = Sse2.LoadVector128((byte*)(src + (i * bytesoftype) + (j * sizeof(Vector128<byte>))));
                xmm1[j] = Sse2.Shuffle(xmm0[j].AsInt32(), 0xd8).AsByte();
                xmm0[j] = Sse2.Shuffle(xmm0[j].AsInt32(), 0x8d).AsByte();
                xmm0[j] = Sse2.UnpackLow(xmm1[j], xmm0[j]);
                xmm1[j] = Sse2.Shuffle(xmm0[j].AsInt32(), 0x04e).AsByte();
                xmm0[j] = Sse2.UnpackLow(xmm0[j].AsInt16(), xmm1[j].AsInt16()).AsByte();
            }
            /* Transpose double words */
            for (j = 0; j < 2; j++)
            {
                xmm1[j * 2] = Sse2.UnpackLow(xmm0[j * 2].AsInt32(), xmm0[j * 2 + 1].AsInt32()).AsByte();
                xmm1[j * 2 + 1] = Sse2.UnpackHigh(xmm0[j * 2].AsInt32(), xmm0[j * 2 + 1].AsInt32()).AsByte();
            }
            /* Transpose quad words */
            for (j = 0; j < 2; j++)
            {
                xmm0[j * 2] = Sse2.UnpackLow(xmm1[j].AsInt64(), xmm1[j + 2].AsInt64()).AsByte();
                xmm0[j * 2 + 1] = Sse2.UnpackHigh(xmm1[j].AsInt64(), xmm1[j + 2].AsInt64()).AsByte();
            }
            /* Store the result vectors */
            dest_for_ith_element = dest + i;
            for (j = 0; j < 4; j++)
            {
                Sse2.Store((byte*)(dest_for_ith_element + (j * total_elements)), xmm0[j]);
            }
        }
    }

    /* Routine optimized for shuffling a buffer for a type size of 8 bytes. */
    private static unsafe void shuffle8_sse2(byte* dest, byte* src,
                    int vectorizable_elements, int total_elements)
    {
        int bytesoftype = 8;
        int j;
        int k, l;
        byte* dest_for_jth_element;
        var xmm0 = new Vector128<byte>[8];
        var xmm1 = new Vector128<byte>[8];

        for (j = 0; j < vectorizable_elements; j += sizeof(Vector128<byte>))
        {
            /* Fetch 16 elements (128 bytes) then transpose bytes. */
            for (k = 0; k < 8; k++)
            {
                xmm0[k] = Sse2.LoadVector128((byte*)(src + (j * bytesoftype) + (k * sizeof(Vector128<byte>))));
                xmm1[k] = Sse2.Shuffle(xmm0[k].AsInt32(), 0x4e).AsByte();
                xmm1[k] = Sse2.UnpackLow(xmm0[k], xmm1[k]);
            }
            /* Transpose words */
            for (k = 0, l = 0; k < 4; k++, l += 2)
            {
                xmm0[k * 2] = Sse2.UnpackLow(xmm1[l].AsInt16(), xmm1[l + 1].AsInt16()).AsByte();
                xmm0[k * 2 + 1] = Sse2.UnpackHigh(xmm1[l].AsInt16(), xmm1[l + 1].AsInt16()).AsByte();
            }
            /* Transpose double words */
            for (k = 0, l = 0; k < 4; k++, l++)
            {
                if (k == 2) l += 2;
                xmm1[k * 2] = Sse2.UnpackLow(xmm0[l].AsInt32(), xmm0[l + 2].AsInt32()).AsByte();
                xmm1[k * 2 + 1] = Sse2.UnpackHigh(xmm0[l].AsInt32(), xmm0[l + 2].AsInt32()).AsByte();
            }
            /* Transpose quad words */
            for (k = 0; k < 4; k++)
            {
                xmm0[k * 2] = Sse2.UnpackLow(xmm1[k].AsInt64(), xmm1[k + 4].AsInt64()).AsByte();
                xmm0[k * 2 + 1] = Sse2.UnpackHigh(xmm1[k].AsInt64(), xmm1[k + 4].AsInt64()).AsByte();
            }
            /* Store the result vectors */
            dest_for_jth_element = dest + j;
            for (k = 0; k < 8; k++)
            {
                Sse2.Store((byte*)(dest_for_jth_element + (k * total_elements)), xmm0[k]);
            }
        }
    }

    /* Routine optimized for shuffling a buffer for a type size of 16 bytes. */
    private static unsafe void shuffle16_sse2(byte* dest, byte* src,
                    int vectorizable_elements, int total_elements)
    {
        int bytesoftype = 16;
        int j;
        int k, l;
        byte* dest_for_jth_element;
        var xmm0 = new Vector128<byte>[16];
        var xmm1 = new Vector128<byte>[16];

        for (j = 0; j < vectorizable_elements; j += sizeof(Vector128<byte>))
        {
            /* Fetch 16 elements (256 bytes). */
            for (k = 0; k < 16; k++)
            {
                xmm0[k] = Sse2.LoadVector128((byte*)(src + (j * bytesoftype) + (k * sizeof(Vector128<byte>))));
            }
            /* Transpose bytes */
            for (k = 0, l = 0; k < 8; k++, l += 2)
            {
                xmm1[k * 2] = Sse2.UnpackLow(xmm0[l], xmm0[l + 1]);
                xmm1[k * 2 + 1] = Sse2.UnpackHigh(xmm0[l], xmm0[l + 1]);
            }
            /* Transpose words */
            for (k = 0, l = -2; k < 8; k++, l++)
            {
                if ((k % 2) == 0) l += 2;
                xmm0[k * 2] = Sse2.UnpackLow(xmm1[l].AsInt16(), xmm1[l + 2].AsInt16()).AsByte();
                xmm0[k * 2 + 1] = Sse2.UnpackHigh(xmm1[l].AsInt16(), xmm1[l + 2].AsInt16()).AsByte();
            }
            /* Transpose double words */
            for (k = 0, l = -4; k < 8; k++, l++)
            {
                if ((k % 4) == 0) l += 4;
                xmm1[k * 2] = Sse2.UnpackLow(xmm0[l].AsInt32(), xmm0[l + 4].AsInt32()).AsByte();
                xmm1[k * 2 + 1] = Sse2.UnpackHigh(xmm0[l].AsInt32(), xmm0[l + 4].AsInt32()).AsByte();
            }
            /* Transpose quad words */
            for (k = 0; k < 8; k++)
            {
                xmm0[k * 2] = Sse2.UnpackLow(xmm1[k].AsInt64(), xmm1[k + 8].AsInt64()).AsByte();
                xmm0[k * 2 + 1] = Sse2.UnpackHigh(xmm1[k].AsInt64(), xmm1[k + 8].AsInt64()).AsByte();
            }
            /* Store the result vectors */
            dest_for_jth_element = dest + j;
            for (k = 0; k < 16; k++)
            {
                Sse2.Store((byte*)(dest_for_jth_element + (k * total_elements)), xmm0[k]);
            }
        }
    }

    /* Routine optimized for shuffling a buffer for a type size larger than 16 bytes. */
    private static unsafe void shuffle16_tiled_sse2(byte* dest, byte* src,
                            int vectorizable_elements, int total_elements, int bytesoftype)
    {
        int j;
        int vecs_per_el_rem = bytesoftype % sizeof(Vector128<byte>);
        int k, l;
        byte* dest_for_jth_element;
        var xmm0 = new Vector128<byte>[16];
        var xmm1 = new Vector128<byte>[16];

        for (j = 0; j < vectorizable_elements; j += sizeof(Vector128<byte>))
        {
            /* Advance the offset into the type by the vector size (in bytes), unless this is
            the initial iteration and the type size is not a multiple of the vector size.
            In that case, only advance by the number of bytes necessary so that the number
            of remaining bytes in the type will be a multiple of the vector size. */
            int offset_into_type;
            for (offset_into_type = 0; offset_into_type < bytesoftype;
                    offset_into_type += (offset_into_type == 0 &&
                                        vecs_per_el_rem > 0 ? vecs_per_el_rem : (int)sizeof(Vector128<byte>)))
            {

                /* Fetch elements in groups of 256 bytes */
                byte* src_with_offset = src + offset_into_type;
                for (k = 0; k < 16; k++)
                {
                    xmm0[k] = Sse2.LoadVector128((byte*)(src_with_offset + (j + k) * bytesoftype));
                }
                /* Transpose bytes */
                for (k = 0, l = 0; k < 8; k++, l += 2)
                {
                    xmm1[k * 2] = Sse2.UnpackLow(xmm0[l], xmm0[l + 1]);
                    xmm1[k * 2 + 1] = Sse2.UnpackHigh(xmm0[l], xmm0[l + 1]);
                }
                /* Transpose words */
                for (k = 0, l = -2; k < 8; k++, l++)
                {
                    if ((k % 2) == 0) l += 2;
                    xmm0[k * 2] = Sse2.UnpackLow(xmm1[l].AsInt16(), xmm1[l + 2].AsInt16()).AsByte();
                    xmm0[k * 2 + 1] = Sse2.UnpackHigh(xmm1[l].AsInt16(), xmm1[l + 2].AsInt16()).AsByte();
                }
                /* Transpose double words */
                for (k = 0, l = -4; k < 8; k++, l++)
                {
                    if ((k % 4) == 0) l += 4;
                    xmm1[k * 2] = Sse2.UnpackLow(xmm0[l].AsInt32(), xmm0[l + 4].AsInt32()).AsByte();
                    xmm1[k * 2 + 1] = Sse2.UnpackHigh(xmm0[l].AsInt32(), xmm0[l + 4].AsInt32()).AsByte();
                }
                /* Transpose quad words */
                for (k = 0; k < 8; k++)
                {
                    xmm0[k * 2] = Sse2.UnpackLow(xmm1[k].AsInt64(), xmm1[k + 8].AsInt64()).AsByte();
                    xmm0[k * 2 + 1] = Sse2.UnpackHigh(xmm1[k].AsInt64(), xmm1[k + 8].AsInt64()).AsByte();
                }
                /* Store the result vectors */
                dest_for_jth_element = dest + j;
                for (k = 0; k < 16; k++)
                {
                    Sse2.Store((byte*)(dest_for_jth_element + (total_elements * (offset_into_type + k))), xmm0[k]);
                }
            }
        }
    }

    /* Routine optimized for unshuffling a buffer for a type size of 2 bytes. */
    private static unsafe void unshuffle2_sse2(byte* dest, byte* src,
                    int vectorizable_elements, int total_elements)
    {
        int bytesoftype = 2;
        int i;
        int j;
        var xmm0 = new Vector128<byte>[2];
        var xmm1 = new Vector128<byte>[2];

        for (i = 0; i < vectorizable_elements; i += sizeof(Vector128<byte>))
        {
            /* Load 16 elements (32 bytes) into 2 XMM registers. */
            byte* src_for_ith_element = src + i;
            for (j = 0; j < 2; j++)
            {
                xmm0[j] = Sse2.LoadVector128((byte*)(src_for_ith_element + (j * total_elements)));
            }
            /* Shuffle bytes */
            /* Compute the low 32 bytes */
            xmm1[0] = Sse2.UnpackLow(xmm0[0], xmm0[1]);
            /* Compute the hi 32 bytes */
            xmm1[1] = Sse2.UnpackHigh(xmm0[0], xmm0[1]);
            /* Store the result vectors in proper order */
            Sse2.Store((byte*)(dest + (i * bytesoftype) + (0 * sizeof(Vector128<byte>))), xmm1[0]);
            Sse2.Store((byte*)(dest + (i * bytesoftype) + (1 * sizeof(Vector128<byte>))), xmm1[1]);
        }
    }

    /* Routine optimized for unshuffling a buffer for a type size of 4 bytes. */
    private static unsafe void unshuffle4_sse2(byte* dest, byte* src,
                    int vectorizable_elements, int total_elements)
    {
        int bytesoftype = 4;
        int i;
        int j;
        var xmm0 = new Vector128<byte>[4];
        var xmm1 = new Vector128<byte>[4];

        for (i = 0; i < vectorizable_elements; i += sizeof(Vector128<byte>))
        {
            /* Load 16 elements (64 bytes) into 4 XMM registers. */
            byte* src_for_ith_element = src + i;
            for (j = 0; j < 4; j++)
            {
                xmm0[j] = Sse2.LoadVector128((byte*)(src_for_ith_element + (j * total_elements)));
            }
            /* Shuffle bytes */
            for (j = 0; j < 2; j++)
            {
                /* Compute the low 32 bytes */
                xmm1[j] = Sse2.UnpackLow(xmm0[j * 2], xmm0[j * 2 + 1]);
                /* Compute the hi 32 bytes */
                xmm1[2 + j] = Sse2.UnpackHigh(xmm0[j * 2], xmm0[j * 2 + 1]);
            }
            /* Shuffle 2-byte words */
            for (j = 0; j < 2; j++)
            {
                /* Compute the low 32 bytes */
                xmm0[j] = Sse2.UnpackLow(xmm1[j * 2].AsInt16(), xmm1[j * 2 + 1].AsInt16()).AsByte();
                /* Compute the hi 32 bytes */
                xmm0[2 + j] = Sse2.UnpackHigh(xmm1[j * 2].AsInt16(), xmm1[j * 2 + 1].AsInt16()).AsByte();
            }
            /* Store the result vectors in proper order */
            Sse2.Store((byte*)(dest + (i * bytesoftype) + (0 * sizeof(Vector128<byte>))), xmm0[0]);
            Sse2.Store((byte*)(dest + (i * bytesoftype) + (1 * sizeof(Vector128<byte>))), xmm0[2]);
            Sse2.Store((byte*)(dest + (i * bytesoftype) + (2 * sizeof(Vector128<byte>))), xmm0[1]);
            Sse2.Store((byte*)(dest + (i * bytesoftype) + (3 * sizeof(Vector128<byte>))), xmm0[3]);
        }
    }

    /* Routine optimized for unshuffling a buffer for a type size of 8 bytes. */
    private static unsafe void unshuffle8_sse2(byte* dest, byte* src,
                    int vectorizable_elements, int total_elements)
    {
        int bytesoftype = 8;
        int i;
        int j;
        var xmm0 = new Vector128<byte>[8];
        var xmm1 = new Vector128<byte>[8];

        for (i = 0; i < vectorizable_elements; i += sizeof(Vector128<byte>))
        {
            /* Load 16 elements (128 bytes) into 8 XMM registers. */
            byte* src_for_ith_element = src + i;
            for (j = 0; j < 8; j++)
            {
                xmm0[j] = Sse2.LoadVector128((byte*)(src_for_ith_element + (j * total_elements)));
            }
            /* Shuffle bytes */
            for (j = 0; j < 4; j++)
            {
                /* Compute the low 32 bytes */
                xmm1[j] = Sse2.UnpackLow(xmm0[j * 2], xmm0[j * 2 + 1]);
                /* Compute the hi 32 bytes */
                xmm1[4 + j] = Sse2.UnpackHigh(xmm0[j * 2], xmm0[j * 2 + 1]);
            }
            /* Shuffle 2-byte words */
            for (j = 0; j < 4; j++)
            {
                /* Compute the low 32 bytes */
                xmm0[j] = Sse2.UnpackLow(xmm1[j * 2].AsInt16(), xmm1[j * 2 + 1].AsInt16()).AsByte();
                /* Compute the hi 32 bytes */
                xmm0[4 + j] = Sse2.UnpackHigh(xmm1[j * 2].AsInt16(), xmm1[j * 2 + 1].AsInt16()).AsByte();
            }
            /* Shuffle 4-byte dwords */
            for (j = 0; j < 4; j++)
            {
                /* Compute the low 32 bytes */
                xmm1[j] = Sse2.UnpackLow(xmm0[j * 2].AsInt32(), xmm0[j * 2 + 1].AsInt32()).AsByte();
                /* Compute the hi 32 bytes */
                xmm1[4 + j] = Sse2.UnpackHigh(xmm0[j * 2].AsInt32(), xmm0[j * 2 + 1].AsInt32()).AsByte();
            }
            /* Store the result vectors in proper order */
            Sse2.Store((byte*)(dest + (i * bytesoftype) + (0 * sizeof(Vector128<byte>))), xmm1[0]);
            Sse2.Store((byte*)(dest + (i * bytesoftype) + (1 * sizeof(Vector128<byte>))), xmm1[4]);
            Sse2.Store((byte*)(dest + (i * bytesoftype) + (2 * sizeof(Vector128<byte>))), xmm1[2]);
            Sse2.Store((byte*)(dest + (i * bytesoftype) + (3 * sizeof(Vector128<byte>))), xmm1[6]);
            Sse2.Store((byte*)(dest + (i * bytesoftype) + (4 * sizeof(Vector128<byte>))), xmm1[1]);
            Sse2.Store((byte*)(dest + (i * bytesoftype) + (5 * sizeof(Vector128<byte>))), xmm1[5]);
            Sse2.Store((byte*)(dest + (i * bytesoftype) + (6 * sizeof(Vector128<byte>))), xmm1[3]);
            Sse2.Store((byte*)(dest + (i * bytesoftype) + (7 * sizeof(Vector128<byte>))), xmm1[7]);
        }
    }

    /* Routine optimized for unshuffling a buffer for a type size of 16 bytes. */
    private static unsafe void unshuffle16_sse2(byte* dest, byte* src,
                        int vectorizable_elements, int total_elements)
    {
        int bytesoftype = 16;
        int i;
        int j;
        var xmm1 = new Vector128<byte>[16];
        var xmm2 = new Vector128<byte>[16];

        for (i = 0; i < vectorizable_elements; i += sizeof(Vector128<byte>))
        {
            /* Load 16 elements (256 bytes) into 16 XMM registers. */
            byte* src_for_ith_element = src + i;
            for (j = 0; j < 16; j++)
            {
                xmm1[j] = Sse2.LoadVector128((byte*)(src_for_ith_element + (j * total_elements)));
            }
            /* Shuffle bytes */
            for (j = 0; j < 8; j++)
            {
                /* Compute the low 32 bytes */
                xmm2[j] = Sse2.UnpackLow(xmm1[j * 2], xmm1[j * 2 + 1]);
                /* Compute the hi 32 bytes */
                xmm2[8 + j] = Sse2.UnpackHigh(xmm1[j * 2], xmm1[j * 2 + 1]);
            }
            /* Shuffle 2-byte words */
            for (j = 0; j < 8; j++)
            {
                /* Compute the low 32 bytes */
                xmm1[j] = Sse2.UnpackLow(xmm2[j * 2].AsInt16(), xmm2[j * 2 + 1].AsInt16()).AsByte();
                /* Compute the hi 32 bytes */
                xmm1[8 + j] = Sse2.UnpackHigh(xmm2[j * 2].AsInt16(), xmm2[j * 2 + 1].AsInt16()).AsByte();
            }
            /* Shuffle 4-byte dwords */
            for (j = 0; j < 8; j++)
            {
                /* Compute the low 32 bytes */
                xmm2[j] = Sse2.UnpackLow(xmm1[j * 2].AsInt32(), xmm1[j * 2 + 1].AsInt32()).AsByte();
                /* Compute the hi 32 bytes */
                xmm2[8 + j] = Sse2.UnpackHigh(xmm1[j * 2].AsInt32(), xmm1[j * 2 + 1].AsInt32()).AsByte();
            }
            /* Shuffle 8-byte qwords */
            for (j = 0; j < 8; j++)
            {
                /* Compute the low 32 bytes */
                xmm1[j] = Sse2.UnpackLow(xmm2[j * 2].AsInt64(), xmm2[j * 2 + 1].AsInt64()).AsByte();
                /* Compute the hi 32 bytes */
                xmm1[8 + j] = Sse2.UnpackHigh(xmm2[j * 2].AsInt64(), xmm2[j * 2 + 1].AsInt64()).AsByte();
            }

            /* Store the result vectors in proper order */
            Sse2.Store((byte*)(dest + (i * bytesoftype) + (0 * sizeof(Vector128<byte>))), xmm1[0]);
            Sse2.Store((byte*)(dest + (i * bytesoftype) + (1 * sizeof(Vector128<byte>))), xmm1[8]);
            Sse2.Store((byte*)(dest + (i * bytesoftype) + (2 * sizeof(Vector128<byte>))), xmm1[4]);
            Sse2.Store((byte*)(dest + (i * bytesoftype) + (3 * sizeof(Vector128<byte>))), xmm1[12]);
            Sse2.Store((byte*)(dest + (i * bytesoftype) + (4 * sizeof(Vector128<byte>))), xmm1[2]);
            Sse2.Store((byte*)(dest + (i * bytesoftype) + (5 * sizeof(Vector128<byte>))), xmm1[10]);
            Sse2.Store((byte*)(dest + (i * bytesoftype) + (6 * sizeof(Vector128<byte>))), xmm1[6]);
            Sse2.Store((byte*)(dest + (i * bytesoftype) + (7 * sizeof(Vector128<byte>))), xmm1[14]);
            Sse2.Store((byte*)(dest + (i * bytesoftype) + (8 * sizeof(Vector128<byte>))), xmm1[1]);
            Sse2.Store((byte*)(dest + (i * bytesoftype) + (9 * sizeof(Vector128<byte>))), xmm1[9]);
            Sse2.Store((byte*)(dest + (i * bytesoftype) + (10 * sizeof(Vector128<byte>))), xmm1[5]);
            Sse2.Store((byte*)(dest + (i * bytesoftype) + (11 * sizeof(Vector128<byte>))), xmm1[13]);
            Sse2.Store((byte*)(dest + (i * bytesoftype) + (12 * sizeof(Vector128<byte>))), xmm1[3]);
            Sse2.Store((byte*)(dest + (i * bytesoftype) + (13 * sizeof(Vector128<byte>))), xmm1[11]);
            Sse2.Store((byte*)(dest + (i * bytesoftype) + (14 * sizeof(Vector128<byte>))), xmm1[7]);
            Sse2.Store((byte*)(dest + (i * bytesoftype) + (15 * sizeof(Vector128<byte>))), xmm1[15]);
        }
    }

    /* Routine optimized for unshuffling a buffer for a type size larger than 16 bytes. */
    private static unsafe void unshuffle16_tiled_sse2(byte* dest, byte* orig,
                            int vectorizable_elements, int total_elements, int bytesoftype)
    {
        int i;
        int vecs_per_el_rem = bytesoftype % sizeof(Vector128<byte>);

        int j;
        byte* dest_with_offset;
        var xmm1 = new Vector128<byte>[16];
        var xmm2 = new Vector128<byte>[16];

        /* The unshuffle loops are inverted (compared to shuffle_tiled16_sse2)
            to optimize cache utilization. */
        int offset_into_type;
        for (offset_into_type = 0; offset_into_type < bytesoftype;
                offset_into_type += (offset_into_type == 0 &&
                    vecs_per_el_rem > 0 ? vecs_per_el_rem : (int)sizeof(Vector128<byte>)))
        {
            for (i = 0; i < vectorizable_elements; i += sizeof(Vector128<byte>))
            {
                /* Load the first 128 bytes in 16 XMM registers */
                byte* src_for_ith_element = orig + i;
                for (j = 0; j < 16; j++)
                {
                    xmm1[j] = Sse2.LoadVector128((byte*)(src_for_ith_element + (total_elements * (offset_into_type + j))));
                }
                /* Shuffle bytes */
                for (j = 0; j < 8; j++)
                {
                    /* Compute the low 32 bytes */
                    xmm2[j] = Sse2.UnpackLow(xmm1[j * 2], xmm1[j * 2 + 1]);
                    /* Compute the hi 32 bytes */
                    xmm2[8 + j] = Sse2.UnpackHigh(xmm1[j * 2], xmm1[j * 2 + 1]);
                }
                /* Shuffle 2-byte words */
                for (j = 0; j < 8; j++)
                {
                    /* Compute the low 32 bytes */
                    xmm1[j] = Sse2.UnpackLow(xmm2[j * 2].AsInt16(), xmm2[j * 2 + 1].AsInt16()).AsByte();
                    /* Compute the hi 32 bytes */
                    xmm1[8 + j] = Sse2.UnpackHigh(xmm2[j * 2].AsInt16(), xmm2[j * 2 + 1].AsInt16()).AsByte();
                }
                /* Shuffle 4-byte dwords */
                for (j = 0; j < 8; j++)
                {
                    /* Compute the low 32 bytes */
                    xmm2[j] = Sse2.UnpackLow(xmm1[j * 2].AsInt32(), xmm1[j * 2 + 1].AsInt32()).AsByte();
                    /* Compute the hi 32 bytes */
                    xmm2[8 + j] = Sse2.UnpackHigh(xmm1[j * 2].AsInt32(), xmm1[j * 2 + 1].AsInt32()).AsByte();
                }
                /* Shuffle 8-byte qwords */
                for (j = 0; j < 8; j++)
                {
                    /* Compute the low 32 bytes */
                    xmm1[j] = Sse2.UnpackLow(xmm2[j * 2].AsInt64(), xmm2[j * 2 + 1].AsInt64()).AsByte();
                    /* Compute the hi 32 bytes */
                    xmm1[8 + j] = Sse2.UnpackHigh(xmm2[j * 2].AsInt64(), xmm2[j * 2 + 1].AsInt64()).AsByte();
                }

                /* Store the result vectors in proper order */
                dest_with_offset = dest + offset_into_type;
                Sse2.Store((byte*)(dest_with_offset + (i + 0) * bytesoftype), xmm1[0]);
                Sse2.Store((byte*)(dest_with_offset + (i + 1) * bytesoftype), xmm1[8]);
                Sse2.Store((byte*)(dest_with_offset + (i + 2) * bytesoftype), xmm1[4]);
                Sse2.Store((byte*)(dest_with_offset + (i + 3) * bytesoftype), xmm1[12]);
                Sse2.Store((byte*)(dest_with_offset + (i + 4) * bytesoftype), xmm1[2]);
                Sse2.Store((byte*)(dest_with_offset + (i + 5) * bytesoftype), xmm1[10]);
                Sse2.Store((byte*)(dest_with_offset + (i + 6) * bytesoftype), xmm1[6]);
                Sse2.Store((byte*)(dest_with_offset + (i + 7) * bytesoftype), xmm1[14]);
                Sse2.Store((byte*)(dest_with_offset + (i + 8) * bytesoftype), xmm1[1]);
                Sse2.Store((byte*)(dest_with_offset + (i + 9) * bytesoftype), xmm1[9]);
                Sse2.Store((byte*)(dest_with_offset + (i + 10) * bytesoftype), xmm1[5]);
                Sse2.Store((byte*)(dest_with_offset + (i + 11) * bytesoftype), xmm1[13]);
                Sse2.Store((byte*)(dest_with_offset + (i + 12) * bytesoftype), xmm1[3]);
                Sse2.Store((byte*)(dest_with_offset + (i + 13) * bytesoftype), xmm1[11]);
                Sse2.Store((byte*)(dest_with_offset + (i + 14) * bytesoftype), xmm1[7]);
                Sse2.Store((byte*)(dest_with_offset + (i + 15) * bytesoftype), xmm1[15]);
            }
        }
    }

    /* Shuffle a block.  This can never fail. */
    private static unsafe void shuffle_sse2(int bytesoftype, int blocksize,
                    byte* _src, byte* _dest)
    {
        int vectorized_chunk_size = bytesoftype * sizeof(Vector128<byte>);
        /* If the blocksize is not a multiple of both the typesize and
            the vector size, round the blocksize down to the next value
            which is a multiple of both. The vectorized shuffle can be
            used for that portion of the data, and the naive implementation
            can be used for the remaining portion. */
        int vectorizable_bytes = blocksize - (blocksize % vectorized_chunk_size);
        int vectorizable_elements = vectorizable_bytes / bytesoftype;
        int total_elements = blocksize / bytesoftype;

        /* If the block size is too small to be vectorized,
            use the generic implementation. */
        if (blocksize < vectorized_chunk_size)
        {
            ShuffleGeneric.shuffle_avx2(bytesoftype, 0, blocksize, _src, _dest);
            return;
        }

        /* Optimized shuffle implementations */
        switch (bytesoftype)
        {
            case 2:
                shuffle2_sse2(_dest, _src, vectorizable_elements, total_elements);
                break;
            case 4:
                shuffle4_sse2(_dest, _src, vectorizable_elements, total_elements);
                break;
            case 8:
                shuffle8_sse2(_dest, _src, vectorizable_elements, total_elements);
                break;
            case 16:
                shuffle16_sse2(_dest, _src, vectorizable_elements, total_elements);
                break;
            default:
                if (bytesoftype > (int)sizeof(Vector128<byte>))
                {
                    shuffle16_tiled_sse2(_dest, _src, vectorizable_elements, total_elements, bytesoftype);
                }
                else
                {
                    /* Non-optimized shuffle */
                    ShuffleGeneric.shuffle_avx2(bytesoftype, 0, blocksize, _src, _dest);
                    /* The non-optimized function covers the whole buffer,
                        so we're done processing here. */
                    return;
                }
                break;
        }

        /* If the buffer had any bytes at the end which couldn't be handled
            by the vectorized implementations, use the non-optimized version
            to finish them up. */
        if (vectorizable_bytes < blocksize)
        {
            ShuffleGeneric.shuffle_avx2(bytesoftype, vectorizable_bytes, blocksize, _src, _dest);
        }
    }

    /* Unshuffle a block.  This can never fail. */
    private static unsafe void unshuffle_sse2(int bytesoftype, int blocksize,
                    byte* _src, byte* _dest)
    {
        int vectorized_chunk_size = bytesoftype * sizeof(Vector128<byte>);
        /* If the blocksize is not a multiple of both the typesize and
            the vector size, round the blocksize down to the next value
            which is a multiple of both. The vectorized unshuffle can be
            used for that portion of the data, and the naive implementation
            can be used for the remaining portion. */
        int vectorizable_bytes = blocksize - (blocksize % vectorized_chunk_size);
        int vectorizable_elements = vectorizable_bytes / bytesoftype;
        int total_elements = blocksize / bytesoftype;

        /* If the block size is too small to be vectorized,
            use the generic implementation. */
        if (blocksize < vectorized_chunk_size)
        {
            ShuffleGeneric.unshuffle_avx2(bytesoftype, 0, blocksize, _src, _dest);
            return;
        }

        /* Optimized unshuffle implementations */
        switch (bytesoftype)
        {
            case 2:
                unshuffle2_sse2(_dest, _src, vectorizable_elements, total_elements);
                break;
            case 4:
                unshuffle4_sse2(_dest, _src, vectorizable_elements, total_elements);
                break;
            case 8:
                unshuffle8_sse2(_dest, _src, vectorizable_elements, total_elements);
                break;
            case 16:
                unshuffle16_sse2(_dest, _src, vectorizable_elements, total_elements);
                break;
            default:
                if (bytesoftype > (int)sizeof(Vector128<byte>))
                {
                    unshuffle16_tiled_sse2(_dest, _src, vectorizable_elements, total_elements, bytesoftype);
                }
                else
                {
                    /* Non-optimized unshuffle */
                    ShuffleGeneric.unshuffle_avx2(bytesoftype, 0, blocksize, _src, _dest);
                    /* The non-optimized function covers the whole buffer,
                        so we're done processing here. */
                    return;
                }
                break;
        }

        /* If the buffer had any bytes at the end which couldn't be handled
            by the vectorized implementations, use the non-optimized version
            to finish them up. */
        if (vectorizable_bytes < blocksize)
        {
            ShuffleGeneric.unshuffle_avx2(bytesoftype, vectorizable_bytes, blocksize, _src, _dest);
        }
    }

}

#endif