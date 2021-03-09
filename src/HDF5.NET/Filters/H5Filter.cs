using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace HDF5.NET
{
    public delegate Memory<byte> FilterFunc(ExtendedFilterFlags flags, uint[] parameters, Memory<byte> buffer);

    public static class H5Filter
    {
        static H5Filter()
        {
            H5Filter.Registrations = new List<H5FilterRegistration>();

            H5Filter.Register(FilterIdentifier.Shuffle, "shuffle", H5Filter.ShuffleFilterFunc);
            H5Filter.Register(FilterIdentifier.Fletcher32, "fletcher", H5Filter.Fletcher32FilterFunc);
            H5Filter.Register(FilterIdentifier.Nbit, "nbit", H5Filter.NbitFilterFunc);
            H5Filter.Register(FilterIdentifier.ScaleOffset, "scaleoffset", H5Filter.NbitFilterFunc);
            H5Filter.Register(FilterIdentifier.Deflate, "deflate", H5Filter.DeflateFilterFunc);
        }

        internal static List<H5FilterRegistration> Registrations { get; set; }

        public static void Register(FilterIdentifier identifier, string name, FilterFunc filterFunc)
        {
            var registration = new H5FilterRegistration(identifier, name, filterFunc);
            H5Filter.Registrations.Add(registration);
        }

        internal static void ExecutePipeline(List<FilterDescription> pipeline,
                                             ExtendedFilterFlags flags,
                                             Memory<byte> filterBuffer,
                                             Memory<byte> resultBuffer)
        {
            // H5Z.c (H5Z_pipeline)

            /* Read */
            if (flags.HasFlag(ExtendedFilterFlags.Reverse))
            {
                for (int i = pipeline.Count; i > 0; --i)
                {
                    var filter = pipeline[i - 1];
                    var registration = H5Filter.Registrations.FirstOrDefault(current => current.Identifier == filter.Identifier);

                    if (registration == null)
                    {
                        var filterName = string.IsNullOrWhiteSpace(filter.Name) ? "unnamed filter" : filter.Name;
                        throw new Exception($"Could not find filter '{filterName}' with ID '{filter.Identifier}'. Make sure the filter has been registered using H5Filter.Register(...).");
                    }

                    var tmpFlags = (ExtendedFilterFlags)((ushort)flags | (ushort)filter.Flags);

                    try
                    {
                        filterBuffer = registration.FilterFunc(tmpFlags, filter.ClientData, filterBuffer);
                    }
                    catch (Exception ex)
                    {
                        throw new Exception("Filter pipeline failed.", ex);
                    }
                }

                filterBuffer[0..resultBuffer.Length]
                    .CopyTo(resultBuffer);
            }
            /* Write */
            else
            {
                throw new Exception("Writing data chunks is not yet supported by HDF5.NET.");
            }
        }

        #region Built-in filters

        private static Memory<byte> ShuffleFilterFunc(ExtendedFilterFlags flags, uint[] parameters, Memory<byte> buffer)
        {
            // read
            if (flags.HasFlag(ExtendedFilterFlags.Reverse))
            {
                var unfilteredBuffer = new byte[buffer.Length];
                ShuffleFilter.Unshuffle((int)parameters[0], buffer.Span, unfilteredBuffer);

                return unfilteredBuffer;
            }

            // write
            else
            {
                var filteredBuffer = new byte[buffer.Length];
                ShuffleFilter.Shuffle((int)parameters[0], buffer.Span, filteredBuffer);

                return filteredBuffer;
            }
        }

        private static Memory<byte> Fletcher32FilterFunc(ExtendedFilterFlags flags, uint[] parameters, Memory<byte> buffer)
        {
            // H5Zfletcher32.c (H5Z_filter_fletcher32)

            // read
            if (flags.HasFlag(ExtendedFilterFlags.Reverse))
            {
                var bufferWithoutChecksum = buffer[0..^4];

                /* Do checksum if it's enabled for read; otherwise skip it
                 * to save performance. */
                if (!flags.HasFlag(ExtendedFilterFlags.SkipEdc))
                {
                    /* Get the stored checksum */
                    var storedFletcher_bytes = buffer.Span[^4..^0];
                    var storedFletcher = BitConverter.ToUInt32(storedFletcher_bytes.ToArray());

                    /* Compute checksum */
                    var fletcher = Fletcher32Generic.Fletcher32(bufferWithoutChecksum.Span);

                    if (fletcher != storedFletcher)
                        throw new Exception("Data error detected by Fletcher32 checksum.");
                }

                return bufferWithoutChecksum;
            }

            // write
            else
            {
                throw new Exception("Writing data chunks is not yet supported by HDF5.NET.");
            }
        }

        private static Memory<byte> NbitFilterFunc(ExtendedFilterFlags flags, uint[] parameters, Memory<byte> buffer)
        {
            throw new Exception($"The filter '{FilterIdentifier.Nbit}' is not yet supported by HDF5.NET.");
        }

        private static Memory<byte> ScaleOffsetFilterFunc(ExtendedFilterFlags flags, uint[] parameters, Memory<byte> buffer)
        {
            throw new Exception($"The filter '{FilterIdentifier.ScaleOffset}' is not yet supported by HDF5.NET.");
        }

        private static Memory<byte> DeflateFilterFunc(ExtendedFilterFlags flags, uint[] parameters, Memory<byte> buffer)
        {
            // Span-based (non-stream) compression APIs
            // https://github.com/dotnet/runtime/issues/39327

            // read
            if (flags.HasFlag(ExtendedFilterFlags.Reverse))
            {
                using var sourceStream = new MemorySpanStream(buffer);

                // skip ZLIB header to get only the DEFLATE stream
                sourceStream.Position = 2;

                using var decompressedStream = new MemoryStream(buffer.Length /* minimum size to expect */);
                using var decompressionStream = new DeflateStream(sourceStream, CompressionMode.Decompress);
                decompressionStream.CopyTo(decompressedStream);

                return decompressedStream
                    .GetBuffer()
                    .AsMemory();
            }

            // write
            else
            {
                // https://github.com/dotnet/runtime/issues/2236
                throw new Exception("The .NET Core deflate algorithm is not yet able to write ZLIB data.");
            }
        }

        #endregion
    }
}
