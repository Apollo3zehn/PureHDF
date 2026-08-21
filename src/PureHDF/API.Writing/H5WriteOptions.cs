using System.Reflection;

namespace PureHDF;

/// <summary>
/// Provides options to be used with <see cref="H5File"/>.
/// </summary>
/// <param name="DefaultStringLength">A value that indicates how strings are handled during serialization. A nonzero positve value means that strings are treated as fixed-length strings, otherwise they are variable-length strings. The default value is 0.</param>
/// <param name="MinimumGlobalHeapCollectionSize">A value that indicates the minimum size of a global heap collection in bytes. The default value is 4096 bytes which is the absolute minimum allowed size.</param>
/// <param name="GlobalHeapFlushThreshold">A value that indicates the threshold after which global heap collections will be flushed. The default value is 4096 * 1024 = 4 MB.</param>
/// <param name="UserBlockSize">Set the file user block size, in bytes. The default user block size is 0; it may be set to any power of 2 equal to 512 or greater (512, 1024, 2048, etc.).</param>
/// <param name="PreferCompactDatasetLayout">A value that indicates whether the writer tries to use the compact layout for datasets if the total data size is &lt; 64 kB and it should not be chunked.</param>
/// <param name="IncludeStructFields">A value that indicates whether struct fields are handled during serialization. The default value is <see langword="true"/>.</param>
/// <param name="IncludeStructProperties">A value that indicates whether struct properties are handled during serialization. The default value is <see langword="false"/>.</param>
/// <param name="IncludeClassFields">A value that indicates whether class fields are handled during serialization. The default value is <see langword="false"/>.</param>
/// <param name="IncludeClassProperties">A value that indicates whether class properties are handled during serialization. The default value is <see langword="true"/>.</param>
/// <param name="FieldNameMapper">Maps a <see cref="FieldInfo"/> to the name of the HDF5 member.</param>
/// <param name="FieldStringLengthMapper">Maps a <see cref="FieldInfo"/> of type string to the desired string length.</param>
/// <param name="PropertyNameMapper">Maps a <see cref="PropertyInfo"/> to the name of the HDF5 member.</param>
/// <param name="PropertyStringLengthMapper">Maps a <see cref="PropertyInfo"/> of type string to the desired string length.</param>
/// <param name="Filters">A list of filters and their options to be applied to datasets that have no explicit filters assigned.</param>
public record H5WriteOptions(
    int DefaultStringLength = default,
    int MinimumGlobalHeapCollectionSize = 4096,
    long GlobalHeapFlushThreshold = 4096 * 1024,
    ulong UserBlockSize = 0,
    bool PreferCompactDatasetLayout = true,
    bool IncludeStructFields = true,
    bool IncludeStructProperties = false,
    bool IncludeClassFields = false,
    bool IncludeClassProperties = true,
    Func<FieldInfo, string?>? FieldNameMapper = default,
    Func<FieldInfo, int?>? FieldStringLengthMapper = default,
    Func<PropertyInfo, string?>? PropertyNameMapper = default,
    Func<PropertyInfo, int?>? PropertyStringLengthMapper = default,
    List<H5Filter>? Filters = default
)
{
    // Declared in the body rather than as further positional parameters, deliberately: adding a
    // parameter to a record's primary constructor changes its signature, which is a binary-breaking
    // change for anything compiled against the previous version, while adding a property is not.

    /// <summary>
    /// Where the writer places file structure relative to dataset payload. The default,
    /// <see cref="H5MetadataPlacement.FrontLoaded" />, keeps structure together at the front of the file.
    /// </summary>
    /// <remarks>
    /// Set <see cref="H5MetadataPlacement.Interleaved" /> to allocate in encode order, which adds nothing
    /// to a write. See <see cref="H5MetadataPlacement" /> for what each one buys and costs.
    /// </remarks>
    public H5MetadataPlacement MetadataPlacement { get; init; } = H5MetadataPlacement.FrontLoaded;

    /// <summary>
    /// The size in bytes of a metadata block, used when <see cref="MetadataPlacement" /> is
    /// <see cref="H5MetadataPlacement.Aggregated" />. The default is 8 MB.
    /// </summary>
    /// <remarks>
    /// Larger blocks cluster more structure together and waste more of the final block. A block smaller
    /// than the largest single metadata allocation cannot hold it, and such an allocation falls back to
    /// being placed inline.
    /// </remarks>
    public long MetadataBlockSize { get; init; } = 8 * 1024 * 1024;

    /// <summary>
    /// The size in bytes reserved at the front of the file for structure, used when
    /// <see cref="MetadataPlacement" /> is <see cref="H5MetadataPlacement.FrontLoaded" />. Zero, the
    /// default, means the writer measures it.
    /// </summary>
    /// <remarks>
    /// Measuring means encoding the file once against a stream that discards everything, which yields an
    /// exact figure rather than an estimate because it is the same encoder and allocator. It does not
    /// compress, so on a filtered write it adds a low single-digit percentage; on an unfiltered write the
    /// share is larger, but an unfiltered write is cheap to begin with.
    /// <para>
    /// Set an explicit value to skip that pass. It is also the only way to get the placement right when
    /// using <see cref="H5File.BeginWrite(string, H5WriteOptions?)" /> to write dataset data after the
    /// initial write: the measuring pass cannot know what a caller will write later, so it under-counts
    /// those chunk indexes and the shortfall is placed as
    /// <see cref="H5MetadataPlacement.Aggregated" /> would place it.
    /// </para>
    /// </remarks>
    public long MetadataReservation { get; init; }
}