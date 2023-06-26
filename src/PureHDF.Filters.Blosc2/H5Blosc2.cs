using System.Runtime.InteropServices;
using Blosc2.PInvoke;

namespace PureHDF.Filters;

/// <summary>
/// Contains a function to enable support for the hardware accelerated Blosc2 filter.
/// </summary>
public static class H5Blosc2
{
    /// <summary>
    /// The Blosc2 filter function.
    /// </summary>
    /// <param name="info">The filter info.</param>
    public unsafe static Memory<byte> FilterFunction(FilterInfo info)
    {
        // adapted from https://github.com/Blosc/hdf5-blosc/blob/bd8ee59708f366ac561153858735165d3a543b18/src/blosc_filter.c#L145-L272
        int status = 0;
        uint clevel = 5;
        uint doshuffle = 1;

        var parameters = info.Parameters;

        /* Filter params that are always set */
        var typesize = parameters[2];                   /* The datatype size */
        ulong outbuf_size = parameters[3];              /* Precomputed buffer guess */

        /* Optional params */
        if (parameters.Length >= 5)
            clevel = parameters[4];                     /* The compression level */

        if (parameters.Length >= 6)
            doshuffle = parameters[5];                  /* BLOSC_SHUFFLE, BLOSC_BITSHUFFLE */

        if (parameters.Length >= 7)
        {
            var compcode = (CompressorCodes)parameters[6];  /* The Blosc compressor used */

            /* Check that we actually have support for the compressor code */
            var namePtr = IntPtr.Zero;
            var compressorsPtr = Blosc.blosc2_list_compressors();
            var code = Blosc.blosc2_compcode_to_compname(compcode, ref namePtr);

            if (code == -1)
                throw new Exception($"This Blosc library does not have support for the '{Marshal.PtrToStringAnsi(namePtr)}' compressor, but only for: {Marshal.PtrToStringAnsi(compressorsPtr)}.");
        }

        /* We're decompressing */
        Memory<byte> resultBuffer = default;

        if (info.Flags.HasFlag(H5FilterFlags.Decompress))
        {
            /* Extract the exact outbuf_size from the buffer header.
            *
            * NOTE: the guess value got from "cd_values" corresponds to the
            * uncompressed chunk size but it should not be used in a general
            * cases since other filters in the pipeline can modify the buffere
            *  size.
            */

            fixed (byte* srcPtr = info.SourceBuffer.Span)
            {
                Blosc.blosc1_cbuffer_sizes(new IntPtr(srcPtr), out outbuf_size, out var cbytes, out var blocksize);

                resultBuffer = info.GetBuffer((int)outbuf_size);

                fixed (byte* destPtr = resultBuffer.Span)
                {
                    status = Blosc.blosc2_decompress(
                        src: new IntPtr(srcPtr), 
                        srcsize: info.SourceBuffer.Length, 
                        dest: new IntPtr(destPtr), 
                        destsize: (int)outbuf_size);

                    /* decompression failed */
                    if (status <= 0)
                        throw new Exception("Blosc decompression error.");
                }
            }

            return resultBuffer;
        }

        /* We're compressing */
        else
        {
            throw new Exception("Writing data chunks is not yet supported by PureHDF.");
        }
    }
}