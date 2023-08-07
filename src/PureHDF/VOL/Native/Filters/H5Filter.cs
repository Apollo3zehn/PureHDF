using System.Collections.Concurrent;
using System.IO.Compression;

namespace PureHDF.Filters;

/// <summary>
/// A delegate which describes a filter function.
/// </summary>
/// <param name="Flags">The filter flags.</param>
/// <param name="Parameters">The filter parameters.</param>
/// <param name="ChunkSize">The chunk size.</param>
/// <param name="SourceBuffer">The source buffer.</param>
/// <param name="FinalBuffer">The final buffer. The final buffer is non-default if the current filter is the last one in the pipeline.</param>
/// <returns>The target buffer.</returns>
public record class FilterInfo(
    H5FilterFlags Flags,
    uint[] Parameters,
    int ChunkSize,
    Memory<byte> SourceBuffer,
    Memory<byte> FinalBuffer);

/// <summary>
/// A class to manage filters.
/// </summary>
static partial class H5Filter
{
    #region Constructors

    static H5Filter()
    {
        ResetRegistrations();
    }

    #endregion

    #region Properties

    internal static ConcurrentDictionary<FilterIdentifier, H5FilterRegistration> Registrations { get; set; } = default!;

    #endregion

    #region Methods

    /// <summary>
    /// Registers a new filter.
    /// </summary>
    /// <param name="identifier">The filter identifier.</param>
    /// <param name="name">The filter name.</param>
    /// <param name="filterFunction">The filter function.</param>
    public static void Register(
        H5FilterID identifier,
        string name, 
        Func<FilterInfo, Memory<byte>> filterFunction)
    {
        var registration = new H5FilterRegistration(
            (FilterIdentifier)identifier, 
            name, 
            filterFunction);

        Registrations
            .AddOrUpdate((FilterIdentifier)identifier, registration, (_, oldRegistration) => registration);
    }

    /// <summary>
    /// Resets the list of filter registrations to the default.
    /// </summary>
    public static void ResetRegistrations()
    {
        Registrations = new ConcurrentDictionary<FilterIdentifier, H5FilterRegistration>();

        Register(H5FilterID.Shuffle, "shuffle", ShuffleFilterFuncion);
        Register(H5FilterID.Fletcher32, "fletcher", Fletcher32FilterFuncion);
        Register(H5FilterID.Nbit, "nbit", NbitFilterFuncion);
        Register(H5FilterID.ScaleOffset, "scaleoffset", ScaleOffsetFilterFuncion);
        Register(H5FilterID.Deflate, "deflate", DeflateFilterFuncion);
    }

    internal static void ExecutePipeline(
        FilterDescription[] pipeline,
        uint filterMask,
        H5FilterFlags flags,
        Memory<byte> filterBuffer,
        Memory<byte> resultBuffer)
    {
        // H5Z.c (H5Z_pipeline)

        /* Read */
        if (flags.HasFlag(H5FilterFlags.Decompress))
        {
            for (int i = pipeline.Length; i > 0; --i)
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
                        ChunkSize: resultBuffer.Length,
                        SourceBuffer: filterBuffer,
                        FinalBuffer: isLast ? resultBuffer : default);

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
        var resultBuffer = info.FinalBuffer.Equals(default)
            ? new byte[info.SourceBuffer.Length]
            : info.FinalBuffer;

        // read
        if (info.Flags.HasFlag(H5FilterFlags.Decompress))
            ShuffleFilter.Unshuffle((int)info.Parameters[0], info.SourceBuffer.Span, resultBuffer.Span);

        // write
        else
            ShuffleFilter.Shuffle((int)info.Parameters[0], info.SourceBuffer.Span, resultBuffer.Span);

        return resultBuffer;
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
#if NET6_0_OR_GREATER
            using var sourceStream = new MemorySpanStream(info.SourceBuffer);

            if (info.FinalBuffer.Equals(default))
            {
                using var decompressedStream = new MemoryStream(capacity: info.ChunkSize /* growable stream */);
                using var decompressionStream = new ZLibStream(sourceStream, CompressionMode.Decompress);

                decompressionStream.CopyTo(decompressedStream);

                return decompressedStream
                    .GetBuffer()
                    .AsMemory(0, (int)decompressedStream.Length);
            }

            else
            {
                using var decompressedStream = new MemorySpanStream(info.FinalBuffer);
                using var decompressionStream = new ZLibStream(sourceStream, CompressionMode.Decompress);

                decompressionStream.CopyTo(decompressedStream);

                return info.FinalBuffer;
            }
#else
            using var sourceStream = new MemorySpanStream(info.SourceBuffer);

            // skip ZLIB header to get only the DEFLATE stream
            sourceStream.Seek(2, SeekOrigin.Begin);

            if (info.FinalBuffer.Equals(default))
            {
                using var decompressedStream = new MemoryStream(capacity: info.ChunkSize /* growable stream */);
                using var decompressionStream = new DeflateStream(sourceStream, CompressionMode.Decompress);

                decompressionStream.CopyTo(decompressedStream);

                return decompressedStream
                    .GetBuffer()
                    .AsMemory(0, (int)decompressedStream.Length);
            }

            else
            {
                using var decompressedStream = new MemorySpanStream(info.FinalBuffer);
                using var decompressionStream = new DeflateStream(sourceStream, CompressionMode.Decompress);

                decompressionStream.CopyTo(decompressedStream);

                return info.FinalBuffer;
            }
#endif
        }

        // write
        else
        {
#if NET6_0_OR_GREATER
            throw new NotImplementedException();
#else
            throw new Exception(".NET 6+ is required for zLib write support.");
#endif
        }
    }

    #endregion
}
