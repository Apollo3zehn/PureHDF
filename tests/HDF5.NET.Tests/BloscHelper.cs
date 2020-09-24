using Blosc2.PInvoke;
using System;
using System.Runtime.InteropServices;

namespace HDF5.NET.Tests
{
    public static class BloscHelper
    {
        public static unsafe Memory<byte> FilterFunc(ExtendedFilterFlags flags, uint[] parameters, Memory<byte> buffer)
        {
            byte[] resultBuffer = null;
            int status = 0;
            uint clevel = 5;
            uint doshuffle = 1;
            CompressorCodes compcode;

            /* Filter params that are always set */
            var typesize = parameters[2];           /* The datatype size */
            ulong outbuf_size = parameters[3];      /* Precomputed buffer guess */

            /* Optional params */
            if (parameters.Length >= 5)
                clevel = parameters[4];             /* The compression level */

            if (parameters.Length >= 6)
                doshuffle = parameters[5];          /* BLOSC_SHUFFLE, BLOSC_BITSHUFFLE */

            if (parameters.Length >= 7)
            {
                compcode = (CompressorCodes)parameters[6];            /* The Blosc compressor used */

                /* Check that we actually have support for the compressor code */
                var namePtr = IntPtr.Zero;
                var compressorsPtr = Blosc.blosc_list_compressors();
                var code = Blosc.blosc_compcode_to_compname(compcode, ref namePtr);

                if (code == -1)
                    throw new Exception($"This Blosc library does not have support for the '{Marshal.PtrToStringAnsi(namePtr)}' compressor, but only for: {Marshal.PtrToStringAnsi(compressorsPtr)}.");
            }

            /* We're decompressing */
            if (flags.HasFlag(ExtendedFilterFlags.Reverse))
            {
                /* Extract the exact outbuf_size from the buffer header.
                *
                * NOTE: the guess value got from "cd_values" corresponds to the
                * uncompressed chunk size but it should not be used in a general
                * cases since other filters in the pipeline can modify the buffere
                *  size.
                */

                fixed (byte* srcPtr = buffer.Span)
                {
                    Blosc.blosc_cbuffer_sizes(new IntPtr(srcPtr), out outbuf_size, out var cbytes, out var blocksize);

                    resultBuffer = new byte[outbuf_size];

                    fixed (byte* destPtr = resultBuffer)
                    {
                        status = Blosc.blosc_decompress(new IntPtr(srcPtr), new IntPtr(destPtr), outbuf_size);

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
                throw new Exception("Writing data chunks is not yet supported by HDF5.NET.");
            }
        }
    }
}
