#if NET5_0_OR_GREATER

using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace PureHDF.Filters;

internal class EndiannessConverterAvx2
{
    public static unsafe void Convert(int bytesOfType, Span<byte> source, Span<byte> destination)
    {
        fixed (byte* src = source, dest = destination)
        {
            Convert(bytesOfType, source.Length, src, dest);
        }
    }

    private static unsafe void Convert2(byte* dest, byte* src, int vectorizable_bytes)
    {
        Vector256<byte> ymm0;

        var shmask = Vector256.Create((byte)
          0x01, 0x00, 0x03, 0x02, 0x05, 0x04, 0x07, 0x06,
          0x09, 0x08, 0x0B, 0x0A, 0x0D, 0x0C, 0x0F, 0x0E,
          0x01, 0x00, 0x03, 0x02, 0x05, 0x04, 0x07, 0x06,
          0x09, 0x08, 0x0B, 0x0A, 0x0D, 0x0C, 0x0F, 0x0E);

        for (var j = 0; j < vectorizable_bytes; j += sizeof(Vector256<byte>))
        {
            ymm0 = Avx.LoadVector256(src + j);
            ymm0 = Avx2.Shuffle(ymm0, shmask);
            Avx2.Store(dest + j, ymm0);
        }
    }

    private static unsafe void Convert4(byte* dest, byte* src, int vectorizable_bytes)
    {
        Vector256<byte> ymm0;

        var shmask = Vector256.Create((byte)
          0x03, 0x02, 0x01, 0x00, 0x07, 0x06, 0x05, 0x04,
          0x0B, 0x0A, 0x09, 0x08, 0x0F, 0x0E, 0x0D, 0x0C,
          0x03, 0x02, 0x01, 0x00, 0x07, 0x06, 0x05, 0x04,
          0x0B, 0x0A, 0x09, 0x08, 0x0F, 0x0E, 0x0D, 0x0C);

        for (var j = 0; j < vectorizable_bytes; j += sizeof(Vector256<byte>))
        {
            ymm0 = Avx.LoadVector256(src + j);
            ymm0 = Avx2.Shuffle(ymm0, shmask);
            Avx2.Store(dest + j, ymm0);
        }
    }

    private static unsafe void Convert8(byte* dest, byte* src, int vectorizable_bytes)
    {
        Vector256<byte> ymm0;

        var shmask = Vector256.Create((byte)
          0x07, 0x06, 0x05, 0x04, 0x03, 0x02, 0x01, 0x00,
          0x0F, 0x0E, 0x0D, 0x0C, 0x0B, 0x0A, 0x09, 0x08,
          0x07, 0x06, 0x05, 0x04, 0x03, 0x02, 0x01, 0x00,
          0x0F, 0x0E, 0x0D, 0x0C, 0x0B, 0x0A, 0x09, 0x08);

        for (var j = 0; j < vectorizable_bytes; j += sizeof(Vector256<byte>))
        {
            ymm0 = Avx.LoadVector256(src + j);
            ymm0 = Avx2.Shuffle(ymm0, shmask);
            Avx2.Store(dest + j, ymm0);
        }
    }

    private static unsafe void Convert16(byte* dest, byte* src, int vectorizable_bytes)
    {
        Vector256<byte> ymm0;

        var shmask = Vector256.Create((byte)
          0x0F, 0x0E, 0x0D, 0x0C, 0x0B, 0x0A, 0x09, 0x08,
          0x07, 0x06, 0x05, 0x04, 0x03, 0x02, 0x01, 0x00,
          0x0F, 0x0E, 0x0D, 0x0C, 0x0B, 0x0A, 0x09, 0x08,
          0x07, 0x06, 0x05, 0x04, 0x03, 0x02, 0x01, 0x00);

        for (var j = 0; j < vectorizable_bytes; j += sizeof(Vector256<byte>))
        {
            ymm0 = Avx.LoadVector256(src + j);
            ymm0 = Avx2.Shuffle(ymm0, shmask);
            Avx2.Store(dest + j, ymm0);
        }
    }

    /* Shuffle a block.  This can never fail. */
    private static unsafe void Convert(int bytesOfType, int blocksize, byte* _src, byte* _dest)
    {
        int vectorized_chunk_size = bytesOfType * sizeof(Vector256<byte>);

        /* If the block size is too small to be vectorized,
           use the generic implementation. */
        if (blocksize < vectorized_chunk_size)
        {
            EndiannessConverterGeneric.Convert(bytesOfType, 0, blocksize, _src, _dest);
            return;
        }

        /* If the blocksize is not a multiple of both the typesize and
           the vector size, round the blocksize down to the next value
           which is a multiple of both. The vectorized shuffle can be
           used for that portion of the data, and the naive implementation
           can be used for the remaining portion. */
        int vectorizable_bytes = blocksize - (blocksize % vectorized_chunk_size);

        /* Optimized shuffle implementations */
        switch (bytesOfType)
        {
            case 2:
                EndiannessConverterAvx2.Convert2(_dest, _src, vectorizable_bytes);
                break;
            case 4:
                EndiannessConverterAvx2.Convert4(_dest, _src, vectorizable_bytes);
                break;
            case 8:
                EndiannessConverterAvx2.Convert8(_dest, _src, vectorizable_bytes);
                break;
            case 16:
                EndiannessConverterAvx2.Convert16(_dest, _src, vectorizable_bytes);
                break;
            default:
                // > 16 bytes are not supported by this implementation, fall back to generic version
                vectorizable_bytes = 0;
                break;
        }

        /* If the buffer had any bytes at the end which couldn't be handled
           by the vectorized implementations, use the non-optimized version
           to finish them up. */
        if (vectorizable_bytes < blocksize)
            EndiannessConverterGeneric.Convert(bytesOfType, vectorizable_bytes, blocksize, _src, _dest);
    }
}

#endif
