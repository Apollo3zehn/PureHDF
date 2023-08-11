using System.Collections.Concurrent;
using System.IO.Compression;
using System.Runtime.InteropServices;

namespace PureHDF.Filters;

/// <summary>
/// A delegate which describes a filter function.
/// </summary>
/// <param name="Flags">The filter flags.</param>
/// <param name="Parameters">The filter parameters.</param>
/// <param name="ChunkSize">The chunk size.</param>
/// <param name="Buffer">The source buffer.</param>
/// <returns>The target buffer.</returns>
public record class FilterInfo(
    H5FilterFlags Flags,
    uint[] Parameters,
    int ChunkSize,
    Memory<byte> Buffer);

/// <summary>
/// A filter with associated options.
/// </summary>
/// <param name="Id">The filter identifier.</param>
/// <param name="Options">Optional filter options.</param>
public record class H5Filter(ushort Id, Dictionary<string, object>? Options = default)
{
    #region Operators

    /// <summary>
    /// Converts a filter identifier into a <see cref="H5Filter"/> instance with default filter options.
    /// </summary>
    /// <param name="filterId"></param>
    public static implicit operator H5Filter(ushort filterId) => new(filterId);

    #endregion

    #region Constructors

    static H5Filter()
    {
        ResetRegistrations();
    }

    #endregion

    #region Properties

    internal static ConcurrentDictionary<ushort, IH5Filter> Registrations { get; set; } = default!;

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
            .AddOrUpdate(filter.FilterId, filter, (_, existingFilter) => filter);
    }

    /// <summary>
    /// Restores the default list of filter registrations.
    /// </summary>
    public static void ResetRegistrations()
    {
        Registrations = new ConcurrentDictionary<ushort, IH5Filter>();

        Register(new ShuffleFilter());
        Register(new Fletcher32Filter());
        Register(new NbitFilter());
        Register(new ScaleOffsetFilter());
        Register(new DeflateFilter());
    }

    internal static Memory<byte> ExecutePipeline(
        FilterDescription[] pipeline,
        uint filterMask,
        H5FilterFlags flags,
        int chunkSize,
        Memory<byte> buffer)
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

                try
                {
                    var filterInfo = new FilterInfo(
                        Flags: tmpFlags,
                        Parameters: filter.ClientData,
                        ChunkSize: chunkSize,
                        Buffer: buffer);

                    buffer = registration.Filter(filterInfo);
                }
                catch (Exception ex)
                {
                    throw new Exception("Filter pipeline failed.", ex);
                }
            }
        }

        /* Write */
        else
        {
            for (int i = 0; i < pipeline.Length; i++)
            {
                /* check if filter should be skipped */
                if (((filterMask >> i) & 0x0001) > 0)
                    continue;

                var filter = pipeline[i];

                if (!Registrations.TryGetValue(filter.Identifier, out var registration))
                {
                    var filterName = string.IsNullOrWhiteSpace(filter.Name) ? "unnamed filter" : filter.Name;
                    throw new Exception($"Could not find filter '{filterName}' with ID '{filter.Identifier}'. Make sure the filter has been registered using H5Filter.Register(...).");
                }

                var tmpFlags = (H5FilterFlags)((ushort)flags | (ushort)filter.Flags);

                try
                {
                    var filterInfo = new FilterInfo(
                        Flags: tmpFlags,
                        Parameters: filter.ClientData,
                        ChunkSize: chunkSize,
                        Buffer: buffer);

                    buffer = registration.Filter(filterInfo);
                }
                catch (Exception ex)
                {
                    throw new Exception("Filter pipeline failed.", ex);
                }
            }
        }

        return buffer;
    }

    #endregion
}

#region Built-in filters

/// <summary>
/// Hardware-accelerated Shuffle filter.
/// </summary>
public class ShuffleFilter : IH5Filter
{
    /// <summary>
    /// The Shuffle filter identifier.
    /// </summary>
    public const ushort Id = 2;

    /// <inheritdoc />
    public ushort FilterId => Id;

    /// <inheritdoc />
    public string Name => "shuffle";

    /// <inheritdoc />
    public Memory<byte> Filter(FilterInfo info)
    {
        var sourceBuffer = info.Buffer;
        var resultBuffer = new byte[info.Buffer.Length].AsMemory();

        // read
        if (info.Flags.HasFlag(H5FilterFlags.Decompress))
            Shuffle.DoUnshuffle((int)info.Parameters[0], sourceBuffer.Span, resultBuffer.Span);

        // write
        else
            Shuffle.DoShuffle((int)info.Parameters[0], sourceBuffer.Span, resultBuffer.Span);

        return resultBuffer;
    }

    /// <inheritdoc />
    public uint[] GetParameters(H5Dataset dataset, uint typeSize, Dictionary<string, object>? options)
    {
        if (typeSize == 0)
            throw new Exception("The type size must be > 0.");

        return new uint[] { typeSize };
    }
}

/// <summary>
/// Fletcher-32 checksum filter.
/// </summary>
public class Fletcher32Filter : IH5Filter
{
    /// <summary>
    /// The Fletcher-32 filter identifier.
    /// </summary>
    public const ushort Id = 3;

    /// <inheritdoc />
    public ushort FilterId => Id;

    /// <inheritdoc />
    public string Name => "fletcher";

    /// <inheritdoc />
    public Memory<byte> Filter(FilterInfo info)
    {
        // H5Zfletcher32.c (H5Z_filter_fletcher32)

        // read
        if (info.Flags.HasFlag(H5FilterFlags.Decompress))
        {
            var bufferWithoutChecksum = info.Buffer[0..^4];

            /* Do checksum if it's enabled for read; otherwise skip it
            * to save performance. */
            if (!info.Flags.HasFlag(H5FilterFlags.SkipEdc))
            {
                /* Get the stored checksum */
                var storedFletcher_bytes = info.Buffer.Span[^4..];
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
            /* Compute checksum */
            var fletcher = Fletcher32Generic.Fletcher32(info.Buffer.Span);

            /* Copy original data */
            var resultBuffer = new byte[info.Buffer.Length + sizeof(uint)].AsMemory();
            info.Buffer.CopyTo(resultBuffer);

            /* Copy checksum */
            Span<uint> fletcherBuffer = stackalloc uint[] { fletcher };
            
            MemoryMarshal.AsBytes(fletcherBuffer)
                .CopyTo(resultBuffer.Span[^4..]);

            return resultBuffer;
        }
    }

    /// <inheritdoc />
    public uint[] GetParameters(H5Dataset dataset, uint typeSize, Dictionary<string, object>? options)
    {
        return Array.Empty<uint>();
    }
}

/// <summary>
/// N-Bit filter.
/// </summary>
public class NbitFilter : IH5Filter
{
    /// <summary>
    /// The N-Bit filter identifier.
    /// </summary>
    public const ushort Id = 5;

    /// <inheritdoc />
    public ushort FilterId => Id;

    /// <inheritdoc />
    public string Name => "nbit";

    /// <inheritdoc />
    public Memory<byte> Filter(FilterInfo info)
    {
        throw new Exception($"The filter '{nameof(NbitFilter)}' is not yet supported by PureHDF.");
    }

    /// <inheritdoc />
    public uint[] GetParameters(H5Dataset dataset, uint typeSize, Dictionary<string, object>? options)
    {
        throw new Exception($"The filter '{nameof(NbitFilter)}' is not yet supported by PureHDF.");
    }
}

/// <summary>
/// Scale-Offset filter.
/// </summary>
public class ScaleOffsetFilter : IH5Filter
{
    /// <summary>
    /// The Scale-Offset identifier.
    /// </summary>
    public const ushort Id = 6;

    /// <inheritdoc />
    public ushort FilterId => Id;

    /// <inheritdoc />
    public string Name => "scaleoffset";

    /// <inheritdoc />
    public Memory<byte> Filter(FilterInfo info)
    {
        // read
        if (info.Flags.HasFlag(H5FilterFlags.Decompress))
            return ScaleOffsetGeneric.Decompress(info.Buffer, info.Parameters);

        // write
        else
            throw new Exception("Writing data chunks is not yet supported by PureHDF.");
    }

    /// <inheritdoc />
    public uint[] GetParameters(H5Dataset dataset, uint typeSize, Dictionary<string, object>? options)
    {
        throw new NotImplementedException();
    }
}

#if NET6_0_OR_GREATER
/// <summary>
/// Deflate filter based on <see cref="ZLibStream"/>.
/// </summary>
#else
/// <summary>
/// Deflate filter based on <see cref="DeflateStream"/>.
/// </summary>
#endif
public class DeflateFilter : IH5Filter
{
    /// <summary>
    /// The compression level options key. The compression level must be one of [-1, 0, 1, 9] and the default is -1.
    /// </summary>
    public const string COMPRESSION_LEVEL = "compression-level";

    /// <summary>
    /// The Deflate filter identifier.
    /// </summary>
    public const ushort Id = 1;

    /// <inheritdoc />
    public ushort FilterId => Id;

    /// <inheritdoc />
    public string Name => "deflate";

    /// <inheritdoc />
    public Memory<byte> Filter(FilterInfo info)
    {
        // Span-based (non-stream) compression APIs
        // https://github.com/dotnet/runtime/issues/39327

        // read
        if (info.Flags.HasFlag(H5FilterFlags.Decompress))
        {
#if NET6_0_OR_GREATER
            using var sourceStream = new MemorySpanStream(info.Buffer);
            using var decompressedStream = new MemoryStream(capacity: info.ChunkSize /* minimum size to expect */);
            using var decompressionStream = new ZLibStream(sourceStream, CompressionMode.Decompress);

            decompressionStream.CopyTo(decompressedStream);

            return decompressedStream
                .GetBuffer()
                .AsMemory(0, (int)decompressedStream.Length);
#else
            using var sourceStream = new MemorySpanStream(info.Buffer);

            // skip ZLIB header to get only the DEFLATE stream
            sourceStream.Seek(2, SeekOrigin.Begin);

            using var decompressionStream = new DeflateStream(sourceStream, CompressionMode.Decompress);
            using var decompressedStream = new MemoryStream(capacity: info.ChunkSize /* minimum size to expect */);

            decompressionStream.CopyTo(decompressedStream);

            return decompressedStream
                .GetBuffer()
                .AsMemory(0, (int)decompressedStream.Length);
#endif
        }

        // write
        else
        {
#if NET6_0_OR_GREATER
            using var sourceStream = new MemorySpanStream(info.Buffer);
            using var compressedStream = new MemoryStream(capacity: info.ChunkSize /* maximum size to expect */);

            var compressionLevelValue = (int)info.Parameters[0];

            /* workaround for https://forum.hdfgroup.org/t/is-deflate-filter-compression-level-1-default-supported/11416 */
            if (compressionLevelValue == 6)
                compressionLevelValue = -1;

            var compressionLevel = GetCompressionLevel(unchecked(compressionLevelValue));

            using (var compressionStream = new ZLibStream(
                compressedStream, 
                compressionLevel,
                leaveOpen: true))
            {
                sourceStream.CopyTo(compressionStream);
            };

            return compressedStream
                .GetBuffer()
                .AsMemory(0, (int)compressedStream.Length);
#else
            throw new Exception(".NET 6+ is required for zlib compression support.");
#endif
        }
    }

    /// <inheritdoc />
    public uint[] GetParameters(H5Dataset dataset, uint typeSize, Dictionary<string, object>? options)
    {
#if NET6_0_OR_GREATER
        var value = GetCompressionLevelValue(options);

        /* workaround for https://forum.hdfgroup.org/t/is-deflate-filter-compression-level-1-default-supported/11416 */
        if (value == -1)
            value = 6;

        if (value < -1 || value > 9)
            throw new Exception("The compression level must be one of [-1, 0, 1, 9].");

        return new uint[] { unchecked((uint)value) };
#else
        throw new Exception(".NET 6+ is required for zlib compression support.");
#endif
    }

#if NET6_0_OR_GREATER
    private static int GetCompressionLevelValue(Dictionary<string, object>? options)
    {
        if (
            options is not null && 
            options.TryGetValue(COMPRESSION_LEVEL, out var objectValue))
        {
            if (objectValue is int value)
                return value;

            else
                throw new Exception($"The value of the filter parameter '{COMPRESSION_LEVEL}' must be of type System.Int32.");
        }

        else
        {
            return -1;
        }
    }

    private static CompressionLevel GetCompressionLevel(int value)
    {
        // H5Pset_deflate() (https://docs.hdfgroup.org/hdf5/develop/group___d_c_p_l.html#gaf1f569bfc54552bdb9317d2b63318a0d)
        // http://www.zlib.net/manual.html#Constants
        return value switch
        {
            0 => CompressionLevel.NoCompression,
            1 => CompressionLevel.Fastest,
            9 => CompressionLevel.SmallestSize,
            -1 => CompressionLevel.Optimal,
            _ => throw new Exception("Only compression levels 0, 1, 9 or -1 are supported. Note that the HDF group's HDF5 library does not support the compression level -1."),
        };
    }
#endif
}

#endregion