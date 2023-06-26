using System.Buffers;
using System.Collections.Concurrent;
using System.IO.Compression;

namespace PureHDF.Filters;

static partial class H5Filter
{
    #region Constructors

    static H5Filter()
    {
        Registrations = new ConcurrentDictionary<FilterIdentifier, H5FilterRegistration>();

        Register(H5FilterID.Shuffle, "shuffle", ShuffleFilterFuncion);
        Register(H5FilterID.Fletcher32, "fletcher", Fletcher32FilterFuncion);
        Register(H5FilterID.Nbit, "nbit", NbitFilterFuncion);
        Register(H5FilterID.ScaleOffset, "scaleoffset", ScaleOffsetFilterFuncion);
        Register(H5FilterID.Deflate, "deflate", DeflateFilterFuncion);
    }

    #endregion

    #region Properties

    internal static ConcurrentDictionary<FilterIdentifier, H5FilterRegistration> Registrations { get; set; }

    #endregion

    #region Methods

       internal static void ExecutePipeline(
        List<FilterDescription> pipeline,
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
                var isLast = i == 1;

                try
                {
                    var filterInfo = new FilterInfo(
                        Flags: tmpFlags,
                        Parameters: filter.ClientData,
                        IsLast: isLast,
                        ChunkSize: resultBuffer.Length,
                        SourceBuffer: filterBuffer,
                        GetBuffer: minimumLength => 
                        {
                            /* return result buffer if this is the last filter and it is large enough */
                            if (isLast && minimumLength <= resultBuffer.Length)
                            {
                                return resultBuffer;
                            }

                            /* otherwise, rent a buffer from the memory pool */
                            else
                            {
                                // TODO renting memory is disabled because it is hard to know when to free it:
                                /* I tried to free rented memory but it seems to be
                                 * impossible to determine if the returned memory belongs
                                 * to one of the IMemoryOwners in the memoryOwners variable
                                 */
                                return new byte[minimumLength];
                            }
                        });

                    filterBuffer = registration.FilterFunction(filterInfo);
                }
                catch (Exception ex)
                {
                    throw new Exception("Filter pipeline failed.", ex);
                }
            }

            /* skip data copying if possible */
            if (!filterBuffer.Equals(resultBuffer))
            {
                filterBuffer[0..resultBuffer.Length]
                    .CopyTo(resultBuffer);
            }
        }

        /* Write */
        else
        {
            throw new Exception("Writing data chunks is not yet supported by PureHDF.");
        }
    }

    #endregion

    #region Built-in filters

    private static Memory<byte> ShuffleFilterFuncion(FilterInfo info)
    {
        // read
        if (info.Flags.HasFlag(H5FilterFlags.Decompress))
        {
            var resultBuffer = info.GetBuffer(info.SourceBuffer.Length);
            ShuffleFilter.Unshuffle((int)info.Parameters[0], info.SourceBuffer.Span, resultBuffer.Span);

            return resultBuffer;
        }

        // write
        else
        {
            var filteredBuffer = info.GetBuffer(info.SourceBuffer.Length);
            ShuffleFilter.Shuffle((int)info.Parameters[0], info.SourceBuffer.Span, filteredBuffer.Span);

            return filteredBuffer;
        }
    }

    private static Memory<byte> Fletcher32FilterFuncion(FilterInfo info)
    {
        // H5Zfletcher32.c (H5Z_filter_fletcher32)

        // read
        if (info.Flags.HasFlag(H5FilterFlags.Decompress))
        {
            var bufferWithoutChecksum = info.SourceBuffer[0..^4];

            /* Do checksum if it's enabled for read; otherwise skip it
             * to save performance. */
            if (!info.Flags.HasFlag(H5FilterFlags.SkipEdc))
            {
                /* Get the stored checksum */
                var storedFletcher_bytes = info.SourceBuffer.Span[^4..];
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

    private static Memory<byte> NbitFilterFuncion(FilterInfo info)
    {
        throw new Exception($"The filter '{FilterIdentifier.Nbit}' is not yet supported by PureHDF.");
    }

    private static Memory<byte> ScaleOffsetFilterFuncion(FilterInfo info)
    {
        // read
        if (info.Flags.HasFlag(H5FilterFlags.Decompress))
            return ScaleOffsetGeneric.Decompress(info.SourceBuffer, info.Parameters);

        // write
        else
            throw new Exception("Writing data chunks is not yet supported by PureHDF.");
    }

    private static Memory<byte> DeflateFilterFuncion(FilterInfo info)
    {
        // Span-based (non-stream) compression APIs
        // https://github.com/dotnet/runtime/issues/39327

        // read
        if (info.Flags.HasFlag(H5FilterFlags.Decompress))
        {
            using var sourceStream = new MemorySpanStream(info.SourceBuffer);

            // skip ZLIB header to get only the DEFLATE stream
            sourceStream.Seek(2, SeekOrigin.Begin);

            if (info.IsLast)
            {
                var resultBuffer = info.GetBuffer(info.ChunkSize /* minimum size */);
                using var decompressedStream = new MemorySpanStream(resultBuffer);
                using var decompressionStream = new DeflateStream(sourceStream, CompressionMode.Decompress);

                decompressionStream.CopyTo(decompressedStream);

                return resultBuffer;
            }

            else
            {
                using var decompressedStream = new MemoryStream(capacity: info.ChunkSize /* growable stream */);
                using var decompressionStream = new DeflateStream(sourceStream, CompressionMode.Decompress);

                decompressionStream.CopyTo(decompressedStream);

                return decompressedStream
                    .GetBuffer()
                    .AsMemory(0, (int)decompressedStream.Length);
            }
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
