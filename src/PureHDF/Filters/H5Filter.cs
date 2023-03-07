using System.Collections.Concurrent;
using System.IO.Compression;

namespace PureHDF
{
    static partial class H5Filter
    {
        #region Constructors

        static H5Filter()
        {
            Registrations = new ConcurrentDictionary<FilterIdentifier, H5FilterRegistration>();

            Register(H5FilterID.Shuffle, "shuffle", ShuffleFilterFunc);
            Register(H5FilterID.Fletcher32, "fletcher", Fletcher32FilterFunc);
            Register(H5FilterID.Nbit, "nbit", NbitFilterFunc);
            Register(H5FilterID.ScaleOffset, "scaleoffset", ScaleOffsetFilterFunc);
            Register(H5FilterID.Deflate, "deflate", DeflateFilterFunc);
        }

        #endregion

        #region Properties

        internal static ConcurrentDictionary<FilterIdentifier, H5FilterRegistration> Registrations { get; set; }

        #endregion

        #region Methods

        internal static void ExecutePipeline(List<FilterDescription> pipeline,
                                             uint filterMask,
                                             H5FilterFlags flags,
                                             Memory<byte> filterBuffer,
                                             Memory<byte> resultBuffer)
        {
            // H5Z.c (H5Z_pipeline)

            /* Read */
            if (flags.HasFlag(H5FilterFlags.Decompress))
            {
                for (int i = pipeline.Count; i > 0; --i)
                {
                    /* check if filter should be skipped */
                    if (((filterMask >> i) & 0x0001) > 0)
                        continue;

                    var filter = pipeline[i - 1];

                    if (!Registrations.TryGetValue(filter.Identifier, out var registration))
                    {
                        var filterName = string.IsNullOrWhiteSpace(filter.Name) ? "unnamed filter" : filter.Name;
                        throw new Exception($"Could not find filter '{filterName}' with ID '{filter.Identifier}'. Make sure the filter has been registered using H5Filter.Register(...).");
                    }

                    var tmpFlags = (H5FilterFlags)((ushort)flags | (ushort)filter.Flags);

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
                throw new Exception("Writing data chunks is not yet supported by PureHDF.");
            }
        }

        #endregion

        #region Built-in filters

        private static Memory<byte> ShuffleFilterFunc(H5FilterFlags flags, uint[] parameters, Memory<byte> buffer)
        {
            // read
            if (flags.HasFlag(H5FilterFlags.Decompress))
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

        private static Memory<byte> Fletcher32FilterFunc(H5FilterFlags flags, uint[] parameters, Memory<byte> buffer)
        {
            // H5Zfletcher32.c (H5Z_filter_fletcher32)

            // read
            if (flags.HasFlag(H5FilterFlags.Decompress))
            {
                var bufferWithoutChecksum = buffer[0..^4];

                /* Do checksum if it's enabled for read; otherwise skip it
                 * to save performance. */
                if (!flags.HasFlag(H5FilterFlags.SkipEdc))
                {
                    /* Get the stored checksum */
                    var storedFletcher_bytes = buffer.Span[^4..^0];
                    var storedFletcher = BitConverter.ToUInt32(storedFletcher_bytes.ToArray(), 0);

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
                throw new Exception("Writing data chunks is not yet supported by PureHDF.");
            }
        }

        private static Memory<byte> NbitFilterFunc(H5FilterFlags flags, uint[] parameters, Memory<byte> buffer)
        {
            throw new Exception($"The filter '{FilterIdentifier.Nbit}' is not yet supported by PureHDF.");
        }

        private static Memory<byte> ScaleOffsetFilterFunc(H5FilterFlags flags, uint[] parameters, Memory<byte> buffer)
        {
            // read
            if (flags.HasFlag(H5FilterFlags.Decompress))
            {
                return ScaleOffsetGeneric.Decompress(buffer, parameters);
            }

            // write
            else
            {
                throw new Exception("Writing data chunks is not yet supported by PureHDF.");
            }
        }

        private static Memory<byte> DeflateFilterFunc(H5FilterFlags flags, uint[] parameters, Memory<byte> buffer)
        {
            // Span-based (non-stream) compression APIs
            // https://github.com/dotnet/runtime/issues/39327

            // read
            if (flags.HasFlag(H5FilterFlags.Decompress))
            {
                using var sourceStream = new MemorySpanStream(buffer);

                // skip ZLIB header to get only the DEFLATE stream
                sourceStream.Seek(2, SeekOrigin.Begin);

                using var decompressedStream = new MemoryStream(buffer.Length /* minimum size to expect */);
                using var decompressionStream = new DeflateStream(sourceStream, CompressionMode.Decompress);
                decompressionStream.CopyTo(decompressedStream);

                return decompressedStream
                    .GetBuffer()
                    .AsMemory(0, (int)decompressedStream.Length);
            }

            // write
            else
            {
                // https://github.com/dotnet/runtime/issues/2236
                throw new Exception("The .NET deflate algorithm is not yet able to write ZLIB data.");
            }
        }

        #endregion
    }
}
