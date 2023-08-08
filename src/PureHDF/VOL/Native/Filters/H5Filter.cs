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
/// A filter with associated options.
/// </summary>
/// <param name="FilterId">The filter id.</param>
/// <param name="Options">Optional filter options.</param>
public record class H5Filter(H5FilterID FilterId, Dictionary<string, object>? Options = default)
{
    #region Constructors

    static H5Filter()
    {
        ResetRegistrations();
    }

    #endregion

    #region Properties

    internal static ConcurrentDictionary<FilterIdentifier, IH5Filter> Registrations { get; set; } = default!;

    #endregion

    #region Methods

    /// <summary>
    /// Registers a new filter.
    /// </summary>
    /// <param name="filter">The filter.</param>
    public static void Register(
        IH5Filter filter)
    {
        Registrations
            .AddOrUpdate((FilterIdentifier)filter.Id, filter, (_, existingFilter) => filter);
    }

    /// <summary>
    /// Restores the default list of filter registrations.
    /// </summary>
    public static void ResetRegistrations()
    {
        Registrations = new ConcurrentDictionary<FilterIdentifier, IH5Filter>();

        Register(new ShuffleFilter());
        Register(new Fletcher32Filter());
        Register(new NbitFilter());
        Register(new ScaleOffsetFilter());
        Register(new DeflateFilter());
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

                    filterBuffer = registration.Filter(filterInfo);
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
}

#region Built-in filters

internal class ShuffleFilter : IH5Filter
{
    public H5FilterID Id => H5FilterID.Shuffle;

    public string Name => "shuffle";

    public Memory<byte> Filter(FilterInfo info)
    {
        var resultBuffer = info.FinalBuffer.Equals(default)
            ? new byte[info.SourceBuffer.Length]
            : info.FinalBuffer;

        // read
        if (info.Flags.HasFlag(H5FilterFlags.Decompress))
            Shuffle.DoUnshuffle((int)info.Parameters[0], info.SourceBuffer.Span, resultBuffer.Span);

        // write
        else
            Shuffle.DoShuffle((int)info.Parameters[0], info.SourceBuffer.Span, resultBuffer.Span);

        return resultBuffer;
    }

    public uint[] GetParameters(H5Dataset dataset, uint typeSize, Dictionary<string, object>? options)
    {
        if (typeSize == 0)
            throw new Exception("The type size must be > 0.");

        return new uint[] { typeSize };
    }
}

internal class Fletcher32Filter : IH5Filter
{
    public H5FilterID Id => H5FilterID.Fletcher32;

    public string Name => "fletcher";

    public Memory<byte> Filter(FilterInfo info)
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

    public uint[] GetParameters(H5Dataset dataset, uint typeSize, Dictionary<string, object>? options)
    {
        throw new NotImplementedException();
    }
}

internal class NbitFilter : IH5Filter
{
    public H5FilterID Id => H5FilterID.Nbit;

    public string Name => "nbit";

    public Memory<byte> Filter(FilterInfo info)
    {
        throw new Exception($"The filter '{FilterIdentifier.Nbit}' is not yet supported by PureHDF.");
    }

    public uint[] GetParameters(H5Dataset dataset, uint typeSize, Dictionary<string, object>? options)
    {
        throw new NotImplementedException();
    }
}

internal class ScaleOffsetFilter : IH5Filter
{
    public H5FilterID Id => H5FilterID.ScaleOffset;

    public string Name => "scaleoffset";

    public Memory<byte> Filter(FilterInfo info)
    {
        // read
        if (info.Flags.HasFlag(H5FilterFlags.Decompress))
            return ScaleOffsetGeneric.Decompress(info.SourceBuffer, info.Parameters);

        // write
        else
            throw new Exception("Writing data chunks is not yet supported by PureHDF.");
    }

    public uint[] GetParameters(H5Dataset dataset, uint typeSize, Dictionary<string, object>? options)
    {
        throw new NotImplementedException();
    }
}

internal class DeflateFilter : IH5Filter
{
    public H5FilterID Id => H5FilterID.Deflate;

    public string Name => "deflate";

    public Memory<byte> Filter(FilterInfo info)
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
                using var decompressionStream = new DeflateStream(sourceStream, CompressionMode.Decompress);
                using var decompressedStream = new MemoryStream(capacity: info.ChunkSize /* growable stream */);

                decompressionStream.CopyTo(decompressedStream);

                return decompressedStream
                    .GetBuffer()
                    .AsMemory(0, (int)decompressedStream.Length);
            }

            else
            {
                using var decompressionStream = new DeflateStream(sourceStream, CompressionMode.Decompress);
                using var decompressedStream = new MemorySpanStream(info.FinalBuffer);

                decompressionStream.CopyTo(decompressedStream);

                return info.FinalBuffer;
            }
#endif
        }

        // write
        else
        {
#if NET6_0_OR_GREATER
            using var sourceStream = new MemorySpanStream(info.SourceBuffer);
            using var compressionStream = new ZLibStream(sourceStream, CompressionMode.Compress);
            using var compressedStream = new MemoryStream(capacity: info.ChunkSize /* growable stream */);

            compressionStream.CopyTo(compressedStream);

            return compressedStream
                .GetBuffer()
                .AsMemory(0, (int)compressedStream.Length);
#else
            throw new Exception(".NET 6+ is required for zlib compression support.");
#endif
        }
    }

    public uint[] GetParameters(H5Dataset dataset, uint typeSize, Dictionary<string, object>? options)
    {
        return Array.Empty<uint>();
    }
}

#endregion