using System.Reflection;

namespace PureHDF;

/// <summary>
/// Provides options to be used with <see cref="H5File"/>.
/// </summary>
/// <param name="DefaultStringLength">A value that indicates how strings are handled during serialization. A nonzero positve value means that strings are treated as fixed-length strings, otherwise they are variable-length strings. The default value is 0.</param>
/// <param name="MinimumGlobalHeapCollectionSize">A value that indicates the minimum size of a global heap collection in bytes. The default value is 4096 bytes which is the absolute minimum allowed size.</param>
/// <param name="GlobalHeapFlushThreshold">Unused. Global heap collections are written as soon as they can take no more objects, at the end of <see cref="H5File.BeginWrite(string, H5WriteOptions?)" /> and of each <see cref="H5NativeWriter.Write{T}" />. Kept so that code setting it still compiles.</param>
/// <param name="UserBlockSize">Set the file user block size, in bytes. The default user block size is 0; it may be set to any power of 2 equal to 512 or greater (512, 1024, 2048, etc.).</param>
/// <param name="PreferCompactDatasetLayout">A value that indicates whether the writer tries to use the compact layout for datasets whose data fits <see cref="MaximumCompactDatasetSize" /> and that should not be chunked.</param>
/// <param name="IncludeStructFields">A value that indicates whether struct fields are handled during serialization. The default value is <see langword="true"/>.</param>
/// <param name="IncludeStructProperties">A value that indicates whether struct properties are handled during serialization. The default value is <see langword="false"/>.</param>
/// <param name="IncludeClassFields">A value that indicates whether class fields are handled during serialization. The default value is <see langword="false"/>.</param>
/// <param name="IncludeClassProperties">A value that indicates whether class properties are handled during serialization. The default value is <see langword="true"/>.</param>
/// <param name="FieldNameMapper">Maps a <see cref="FieldInfo"/> to the name of the HDF5 member.</param>
/// <param name="FieldStringLengthMapper">Maps a <see cref="FieldInfo"/> of type string to the desired string length.</param>
/// <param name="PropertyNameMapper">Maps a <see cref="PropertyInfo"/> to the name of the HDF5 member.</param>
/// <param name="PropertyStringLengthMapper">Maps a <see cref="PropertyInfo"/> of type string to the desired string length.</param>
/// <param name="Filters">A list of filters and their options to be applied to datasets that have no explicit filters assigned. Except datasets given with the tree that hold at most 64 bytes, variable-length values included, which are written compact instead when <see cref="MaximumCompactDatasetSize" /> allows it: a filter needs a chunked layout, and compressing a few bytes costs more than it saves. Assign filters to such a dataset itself to have them applied regardless.</param>
/// <param name="AttributeStringLength">
///     How a string attribute is sized. The default, <see cref="H5AttributeStringLength.Measured" />, makes
///     it as wide as its own value.
///     <para>
///         Independent of <see cref="DefaultStringLength" />, which continues to govern datasets and the string
///         members of a compound attribute. Set <see cref="H5AttributeStringLength.Inherit" /> to have
///         attributes follow it as well.
///     </para>
///     <para>
///         Under <see cref="H5AttributeStringLength.Measured" />, an attribute that holds a <see langword="null" />
///         element is written variable-length, since a fixed-length field cannot distinguish <see langword="null" />
///         from an empty string. So is one too large to be stored inline, an attribute message being limited to
///         65535 bytes in total.
///     </para>
/// </param>
/// <param name="StringOverflow">
///     What to do with a string too long for the fixed-length width it is written into. The default,
///     <see cref="H5StringOverflow.Truncate" />, keeps the bytes that fit and discards the rest.
///     <para>
///         Set <see cref="H5StringOverflow.Throw" /> to throw an <see cref="InvalidOperationException" />
///         and fail the write instead. Worth doing wherever declared widths come from data that could grow,
///         since the alternative is losing the excess with no indication - and, because widths are in
///         bytes, losing it mid-character.
///     </para>
///     <para>
///         Applies only where a width is DECLARED - a compound member sized by
///         <see cref="DefaultStringLength" /> or a string length mapper, or an attribute set to
///         <see cref="H5AttributeStringLength.Inherit" />. A measured width is taken from the value itself, so
///         it cannot overflow.
///     </para>
/// </param>
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
    List<H5Filter>? Filters = default,
    H5AttributeStringLength AttributeStringLength = H5AttributeStringLength.Measured,
    H5StringOverflow StringOverflow = H5StringOverflow.Truncate
)
{
    // Declared in the body rather than as further positional parameters, deliberately: adding a
    // parameter to a record's primary constructor changes its signature, which is a binary-breaking
    // change for anything compiled against the previous version, while adding a property is not.

    /// <summary>
    /// Where the writer places file structure relative to dataset payload. The default,
    /// <see cref="H5MetadataPlacement.Interleaved" />, allocates in encode order and adds nothing to a
    /// write.
    /// </summary>
    /// <remarks>
    /// Set <see cref="H5MetadataPlacement.FrontLoaded" /> to keep structure together at the front of the
    /// file, which is what a reader fetching ranges over a high-latency link wants. See
    /// <see cref="H5MetadataPlacement" /> for what each one buys and costs.
    /// </remarks>
    public H5MetadataPlacement MetadataPlacement { get; init; } = H5MetadataPlacement.Interleaved;

    /// <summary>
    /// The largest metadata block the writer will open, in bytes. The default is 8 MB. It applies when
    /// <see cref="MetadataPlacement" /> is <see cref="H5MetadataPlacement.Aggregated" />, and to the
    /// remainder when a <see cref="H5MetadataPlacement.FrontLoaded" /> reservation runs out.
    /// </summary>
    /// <remarks>
    /// A ceiling rather than a fixed size: the first block is small and each one doubles up to this
    /// value, so what a file pays is proportional to the metadata it has rather than a floor it pays
    /// whatever its size. Raising this clusters more structure per block once a file is large enough to
    /// reach it. An allocation larger than this cannot be held by a block at all, and is placed inline.
    /// <para>
    /// Must be greater than zero for any placement other than
    /// <see cref="H5MetadataPlacement.Interleaved" />, which uses no blocks and ignores it.
    /// </para>
    /// </remarks>
    public long MetadataBlockSize { get; init; } = 8 * 1024 * 1024;

    /// <summary>
    /// The size in bytes reserved at the front of the file for structure, used when
    /// <see cref="MetadataPlacement" /> is <see cref="H5MetadataPlacement.FrontLoaded" />. Zero, the
    /// default, means the region is exactly as large as the structure turns out to be.
    /// </summary>
    /// <remarks>
    /// Without a reservation, the writer encodes the structure once, holds it in memory until it is
    /// complete, and then writes it in one run, front to back. Holding it costs memory the size of the
    /// structure until <see cref="H5File.BeginWrite(string, H5WriteOptions?)" /> returns.
    /// <para>
    /// An explicit value writes the structure as it is encoded instead, without holding it, at the cost of
    /// guessing its size: one too large abandons whatever it over-reserved. Data written later through
    /// <see cref="H5File.BeginWrite(string, H5WriteOptions?)" /> needs no allowance either - a chunk
    /// index is sized from the chunk count, which comes from the dimensions rather than from the data,
    /// and a variable-length value's heap space is payload rather than structure, so writing more of it
    /// does not move the total. A reservation that nonetheless falls short spills into blocks, which
    /// loses locality for the remainder rather than failing.
    /// </para>
    /// </remarks>
    public long MetadataReservation { get; init; }

    /// <summary>
    /// The most data, in bytes, a dataset may hold and still be written compact when
    /// <see cref="PreferCompactDatasetLayout" /> is set. The default is 256 bytes; a compact layout can
    /// hold at most 65531.
    /// </summary>
    /// <remarks>
    /// A compact dataset's data is part of its object header, so it is file structure: a reader that
    /// opens the dataset has its data without another request, but one that browses the structure fetches
    /// it too, and a front-loaded file places it with the structure. Lower this to keep structure compact
    /// for browsing. A variable-length dataset counts its values as well as their heap IDs, since its
    /// values are placed with it.
    /// <para>
    /// The default keeps what compact is good for - a scalar, a few values, a short string, which a
    /// reader gets with the header instead of with another request - while bounding what a file of many
    /// such datasets adds to its structure: about twice the structure at most, where the 64 kB a compact
    /// layout can hold turns a file of many datasets of a few kilobytes into structure entirely.
    /// </para>
    /// <para>
    /// Must not be negative. Zero writes no dataset compact.
    /// </para>
    /// </remarks>
    public int MaximumCompactDatasetSize { get; init; } = 256;

    // An object header message declares its size in two bytes, and a compact layout message spends four
    // of them on its version, layout class and data size.
    internal const int MAXIMUM_COMPACT_DATASET_SIZE = ushort.MaxValue - 4;
}
