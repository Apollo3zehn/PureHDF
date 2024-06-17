using System.Runtime.InteropServices;
using Blosc2.PInvoke;

namespace PureHDF.Filters;

/// <summary>
/// Defined Blosc2 shuffle modes.
/// </summary>
public enum Blosc2ShuffleMode
{
    /// <summary>
    /// Bit-shuffle will be used for buffers with itemsize 1, and byte-shuffle will be used otherwise.
    /// </summary>
    AutoShuffle = -1,

    /// <summary>
    /// No shuffle.
    /// </summary>
    NoShuffle = 0,

    /// <summary>
    /// Byte shuffle.
    /// </summary>
    Shuffle = 1,

    /// <summary>
    /// Bit shuffle.
    /// </summary>
    BitShuffle = 2
}

/// <summary>
/// Blosc2 filter.
/// </summary>
public class Blosc2Filter : IH5Filter
{
    private const int BLOSC2_MAX_OVERHEAD = 32;

    // adapted from https://github.com/Blosc/hdf5-blosc/blob/bd8ee59708f366ac561153858735165d3a543b18/src/blosc_filter.c#L145-L272

    /// <summary>
    /// The compression level options key. The compression level must be in the range of 0 to 9 and the default is 5.
    /// </summary>
    public const string COMPRESSION_LEVEL = "compression-level";

    /// <summary>
    /// The shuffle options key. The default is <see cref="Blosc2ShuffleMode.Shuffle"/>.
    /// </summary>
    public const string SHUFFLE = "shuffle";

    /// <summary>
    /// The compressor code options key. Possible values are blosclz, lz4, lz4hc, zlib or zstd. The default compressor is blosclz.
    /// </summary>
    public const string COMPRESSOR_CODE = "compressor-code";

    /// <summary>
    /// The Blosc2 filter identifier.
    /// </summary>
    public const ushort Id = 32001;

    /// <inheritdoc />
    public ushort FilterId => Id;

    /// <inheritdoc />
    public string Name => "blosc1";

    /// <inheritdoc />
    public unsafe Memory<byte> Filter(FilterInfo info)
    {
        var parameters = info.Parameters;

        int clevel = default;
        int doshuffle = default;
        string compname = string.Empty;

        /* Filter params that are always set */
        var typesize = unchecked((int)parameters[2]);       /* The datatype size */
        var _ = (int)parameters[3];                         /* Precomputed buffer guess */

        /* Optional params */
        if (parameters.Length >= 5)
            clevel = unchecked((int)parameters[4]);         /* The compression level */

        if (parameters.Length >= 6)
            doshuffle = unchecked((int)parameters[5]);      /* BLOSC_SHUFFLE, BLOSC_BITSHUFFLE */

        if (parameters.Length >= 7)
        {
            var compcode = (CompressorCodes)parameters[6];  /* The Blosc compressor used */

            /* Check that we actually have support for the compressor code */
            var compnamePtr = IntPtr.Zero;
            var code = Blosc.blosc2_compcode_to_compname(compcode, ref compnamePtr);
            compname = Marshal.PtrToStringAnsi(compnamePtr);

            if (code == -1)
            {
                var compressorsPtr = Blosc.blosc2_list_compressors();
                throw new Exception($"This Blosc library does not have support for the '{compname}' compressor, but only for: {Marshal.PtrToStringAnsi(compressorsPtr)}.");
            }
        }

        /* We're decompressing */
        if (info.Flags.HasFlag(H5FilterFlags.Decompress))
        {
            fixed (byte* srcPtr = info.Buffer.Span)
            {
                /* Extract the exact outbuf_size from the buffer header.
                 *
                 * NOTE: the guess value got from "cd_values" corresponds to the
                 * uncompressed chunk size but it should not be used in a general
                 * cases since other filters in the pipeline can modify the buffer
                 * size.
                 */
                var status = Blosc.blosc2_cbuffer_sizes(new IntPtr(srcPtr), out var outbuf_size, out var cbytes, out var blocksize);

#if NET5_0_OR_GREATER
                var buffer = GC
                    .AllocateUninitializedArray<byte>(outbuf_size)
                    .AsMemory();
#else
                var buffer = new byte[outbuf_size].AsMemory();
#endif

                if (status < 0)
                    throw new Exception($"Blosc decompression error (status {status}).");

                fixed (byte* destPtr = buffer.Span)
                {
                    var decompressedSize = Blosc.blosc2_decompress(
                        src: new IntPtr(srcPtr),
                        srcsize: info.Buffer.Length,
                        dest: new IntPtr(destPtr),
                        destsize: buffer.Length);

                    /* decompression failed */
                    if (decompressedSize <= 0)
                        throw new Exception($"Blosc decompression error (status {status}).");
                }

                return buffer;
            }
        }

        /* We're compressing */
        else
        {
            /* "The size of the dest buffer. Blosc guarantees that if you set destsize to, 
             *  at least, (nbytes + BLOSC2_MAX_OVERHEAD), the compression will always succeed."
             * - https://www.blosc.org/c-blosc2/reference/blosc1.html#c.blosc1_compress
             */
            var buffer = new byte[info.Buffer.Length + BLOSC2_MAX_OVERHEAD].AsMemory();

            Blosc.blosc1_set_compressor(compname);

            fixed (byte* srcPtr = info.Buffer.Span)
            {
                fixed (byte* destPtr = buffer.Span)
                {
                    var compressedSize = Blosc.blosc2_compress(
                        clevel, doshuffle, typesize,
                        new IntPtr(srcPtr), info.Buffer.Length,
                        new IntPtr(destPtr), buffer.Length);

                    if (compressedSize < 0)
                        throw new Exception($"Blosc compression error (status {compressedSize}).");

                    return buffer.Slice(0, compressedSize);
                }
            }
        }
    }

    /// <inheritdoc />
    public uint[] GetParameters(uint[] chunkDimensions, uint typeSize, Dictionary<string, object>? options)
    {
        var parameters = new uint[7];

        /* Blosc version */
        parameters[0] = 2;

        /* Blosc version format  */
        parameters[1] = 2;

        /* Limit large typesizes (they are pretty expensive to shuffle
        and, in addition, Blosc does not handle typesizes larger than
        256 bytes). */
        if (typeSize > 256)
            typeSize = 1;

        parameters[2] = typeSize;

        /* Get the size of the chunk */
        var chunkSize = typeSize * chunkDimensions
            .Aggregate(1U, (product, dimension) => product * dimension);

        parameters[3] = chunkSize;

        /* compression level */
        var compressionLevel = GetCompressionLevelValue(options);

        if (compressionLevel < 0 || compressionLevel > 9)
            throw new Exception("The compression level must be in the range of 0..9.");

        parameters[4] = (uint)compressionLevel;

        /* shuffle */
        var shuffle = GetShuffleValue(options);

        if (!Enum.IsDefined(typeof(Blosc2ShuffleMode), shuffle))
            throw new Exception($"The shuffle mode {shuffle} is invalid.");

        parameters[5] = unchecked((uint)(int)shuffle);

        /* compressor code */
        var compressorCode = GetCompressorCode(options);
        parameters[6] = (uint)compressorCode;

        return parameters;
    }

    private static int GetCompressionLevelValue(Dictionary<string, object>? options)
    {
        if (
            options is not null &&
            options.TryGetValue(COMPRESSION_LEVEL, out var objectValue))
        {
            if (objectValue is int value)
                return value;

            else
                throw new Exception($"The value of the filter parameter '{COMPRESSION_LEVEL}' must be of type {nameof(Int32)}.");
        }

        else
        {
            return 5;
        }
    }

    private static Blosc2ShuffleMode GetShuffleValue(Dictionary<string, object>? options)
    {
        if (
            options is not null &&
            options.TryGetValue(SHUFFLE, out var objectValue))
        {
            if (objectValue is Blosc2ShuffleMode value)
                return value;

            else
                throw new Exception($"The value of the filter parameter '{SHUFFLE}' must be of type {nameof(Blosc2ShuffleMode)}.");
        }

        else
        {
            return Blosc2ShuffleMode.Shuffle;
        }
    }

    private static int GetCompressorCode(Dictionary<string, object>? options)
    {
        if (
            options is not null &&
            options.TryGetValue(COMPRESSOR_CODE, out var objectValue))
        {
            if (objectValue is string value)
            {
                var code = Blosc.blosc2_compname_to_compcode(value);

                if (code == 1)
                {
                    var compressorsPtr = Blosc.blosc2_list_compressors();
                    throw new Exception($"This Blosc library does not have support for the '{value}' compressor, but only for: {Marshal.PtrToStringAnsi(compressorsPtr)}.");
                }

                return code;
            }

            else
            {
                throw new Exception($"The value of the filter parameter '{COMPRESSOR_CODE}' must be of type {nameof(String)}.");
            }
        }

        else
        {
            return 0; // "blosclz"
        }
    }
}