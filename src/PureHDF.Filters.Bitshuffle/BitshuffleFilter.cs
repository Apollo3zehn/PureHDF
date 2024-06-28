using System.Buffers.Binary;
using Bitshuffle.PInvoke;

namespace PureHDF.Filters;

/// <summary>
/// Available Bitshuffle compression modes.
/// </summary>
public enum BitshuffleCompressionMode : uint
{
    /// <summary>
    /// There will be no compression applied.
    /// </summary>
    NoCompression = 0,

    /// <summary>
    /// LZ4 compression will additionally be applied.
    /// </summary>
    LZ4 = 2
}

/// <summary>
/// Bitshuffle filter based on https://github.com/kiyo-masui/bitshuffle.
/// </summary>
public class BitshuffleFilter : IH5Filter
{
    /*
     * Error codes:
     *      -1    : Failed to allocate memory.
     *      -11   : Missing SSE.
     *      -12   : Missing AVX.
     *      -13   : Missing Arm Neon.
     *      -14   : Missing AVX512.
     *      -80   : Input size not a multiple of 8.
     *      -81   : block_size not multiple of 8.
     *      -91   : Decompression error, wrong number of bytes processed.
     *      -1YYY : Error internal to compression routine with error code -YYY.
    */

    /// <summary>
    /// The block size options key. The block size must be >= 0 and the default is 0 which means the block size is auto-selected.
    /// </summary>
    public const string BLOCK_SIZE = "block-size";

    /// <summary>
    /// The compression mode key. By default no compression will be applied.
    /// </summary>
    public const string COMPRESSION_MODE = "compression-mode";

    /// <summary>
    /// The Bitshuffle filter identifier.
    /// </summary>
    public const ushort Id = 32008;

    /// <inheritdoc />
    public ushort FilterId => Id;

    /// <inheritdoc />
    public string Name => "bitshuffle; see https://github.com/kiyo-masui/bitshuffle";

    /// <inheritdoc />
    public unsafe Memory<byte> Filter(FilterInfo info)
    {
        var sourceBuffer = info.Buffer.Span;
        var elementSize = info.Parameters[2];
        var blockSize = 0U;

        ulong uncompressedBufferSize;
        ulong targetBufferSize;

        long err;

        // Step 1: Parameter preparation

        if (info.Parameters.Length > 4 && info.Parameters[4] == (uint)BitshuffleCompressionMode.LZ4)
        {
            /* We're decompressing */
            if (info.Flags.HasFlag(H5FilterFlags.Decompress))
            {
                // First eight bytes is the number of bytes in the output buffer,
                // little endian.
                uncompressedBufferSize = BinaryPrimitives.ReadUInt64BigEndian(info.Buffer.Span);
                sourceBuffer = sourceBuffer.Slice(sizeof(ulong));

                // Override the block size with the one read from the header.
                blockSize = BinaryPrimitives.ReadUInt32BigEndian(sourceBuffer) / elementSize;
                sourceBuffer = sourceBuffer.Slice(sizeof(uint));

                targetBufferSize = uncompressedBufferSize;
            }

            /* We're compressing */
            else
            {
                uncompressedBufferSize = (ulong)info.Buffer.Length;

                // Pick which compressions library to use
                if(info.Parameters[4] == (uint)BitshuffleCompressionMode.LZ4) {
                    targetBufferSize = (ulong)BitshuffleMethods.bshuf_compress_lz4_bound(
                        (nint)(uncompressedBufferSize / elementSize),
                        (nint)elementSize, 
                        (nint)blockSize
                    ) + 12;
                }
            }
        }

        else
        {
            uncompressedBufferSize = (ulong)info.Buffer.Length;
            targetBufferSize = (ulong)info.Buffer.Length;
        }

        // Step 2: Validation & allocation
        if (uncompressedBufferSize % elementSize != 0)
            throw new Exception("Non integer number of elements.");

        var size = uncompressedBufferSize / elementSize;

        var targetBuffer = GC
            .AllocateUninitializedArray<byte>((int)uncompressedBufferSize)
            .AsMemory();

        var targetBufferSpan = targetBuffer.Span;

        // Step 3: Go!
        fixed (byte* sourceBufferPtr = sourceBuffer, targetBufferPtr = targetBufferSpan)

        if (info.Parameters.Length > 4 && info.Parameters[4] == (uint)BitshuffleCompressionMode.LZ4)
        {
            /* We're decompressing */
            if (info.Flags.HasFlag(H5FilterFlags.Decompress))
            {
                // Bit unshuffle/decompress.
                // Pick which compressions library to use
                if (info.Parameters[4] == (uint)BitshuffleCompressionMode.LZ4) {
                    err = BitshuffleMethods.bshuf_decompress_lz4(
                        sourceBufferPtr, 
                        targetBufferPtr, 
                        (nint)size, 
                        (nint)elementSize, 
                        (nint)blockSize
                    );
                }

                else
                {
                    throw new Exception("This should never happen");
                }
            }

            /* We're compressing */
            else
            {
                // Bit shuffle/compress.
                // Write the header, described in
                // http://www.hdfgroup.org/services/filters/HDF5_LZ4.pdf.
                // Technically we should be using signed integers instead of
                // unsigned ones, however for valid inputs (positive numbers) these
                // have the same representation.
                BinaryPrimitives.WriteUInt64BigEndian(targetBufferSpan, uncompressedBufferSize);
                targetBufferSpan = targetBufferSpan.Slice(sizeof(ulong));

                BinaryPrimitives.WriteUInt32BigEndian(targetBufferSpan, blockSize * elementSize);

                if (info.Parameters[4] == (uint)BitshuffleCompressionMode.LZ4) 
                {
                    err = BitshuffleMethods.bshuf_compress_lz4(
                        sourceBufferPtr, 
                        targetBufferPtr + 12, 
                        (nint)size, 
                        (nint)elementSize, 
                        (nint)blockSize
                    ); 
                }

                else
                {
                    throw new Exception("This should never happen");
                }
            }
        }

        else
        {
            /* We're decompressing */
            if (info.Flags.HasFlag(H5FilterFlags.Decompress))
            {
                err = BitshuffleMethods.bshuf_bitunshuffle(
                    sourceBufferPtr, 
                    targetBufferPtr, 
                    (nint)size, 
                    (nint)elementSize, 
                    (nint)blockSize
                );
            }

            /* We're compressing */
            else
            {
                err = BitshuffleMethods.bshuf_bitshuffle(
                    sourceBufferPtr, 
                    targetBufferPtr, 
                    (nint)size, 
                    (nint)elementSize, 
                    (nint)blockSize); 
            }
        }

        if (err < 0)
            throw new Exception("Error in bitshuffle with error code %d. See https://github.com/kiyo-masui/bitshuffle/blob/b9a1546133959298c56eee686932dbb18ff80f7a/src/bitshuffle.h#L17-L24 for a detailed description.");

        return targetBuffer;
    }

    /// <inheritdoc />
    public uint[] GetParameters(uint[] chunkDimensions, uint typeSize, Dictionary<string, object>? options)
    {
        var majorVersion = 0U;
        var minorVersion = 4U;
        var blockSize = GetBlockSizeValue(options);
        var compressionMode = GetCompressionModeValue(options);

        return [
            majorVersion, 
            minorVersion, 
            typeSize, 
            (uint)blockSize, 
            (uint)compressionMode
        ];
    }

    private static int GetBlockSizeValue(Dictionary<string, object>? options)
    {
        if (
            options is not null &&
            options.TryGetValue(BLOCK_SIZE, out var objectValue))
        {
            if (objectValue is int value)
                return value;

            else
                throw new Exception($"The value of the filter parameter '{BLOCK_SIZE}' must be of type {nameof(Int32)}.");
        }

        else
        {
            return -1;
        }
    }

    private static BitshuffleCompressionMode GetCompressionModeValue(Dictionary<string, object>? options)
    {
        if (
            options is not null &&
            options.TryGetValue(COMPRESSION_MODE, out var objectValue))
        {
            if (objectValue is BitshuffleCompressionMode value)
                return value;

            else
                throw new Exception($"The value of the filter parameter '{COMPRESSION_MODE}' must be of type {nameof(BitshuffleCompressionMode)}.");
        }

        else
        {
            return BitshuffleCompressionMode.NoCompression;
        }
    }
}