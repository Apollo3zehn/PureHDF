using System.Reflection;

namespace PureHDF;

/// <summary>
///     Provides options to be used with <see cref="H5File" />.
/// </summary>
/// <param name="DefaultStringLength">
///     A value that indicates how strings are handled during serialization. A nonzero
///     positve value means that strings are treated as fixed-length strings, otherwise they are variable-length strings.
///     The default value is 0.
/// </param>
/// <param name="MinimumGlobalHeapCollectionSize">
///     A value that indicates the minimum size of a global heap collection in
///     bytes. The default value is 4096 bytes which is the absolute minimum allowed size.
/// </param>
/// <param name="GlobalHeapFlushThreshold">
///     A value that indicates the threshold after which global heap collections will be
///     flushed. The default value is 4096 * 1024 = 4 MB.
/// </param>
/// <param name="UserBlockSize">
///     Set the file user block size, in bytes. The default user block size is 0; it may be set to
///     any power of 2 equal to 512 or greater (512, 1024, 2048, etc.).
/// </param>
/// <param name="PreferCompactDatasetLayout">
///     A value that indicates whether the writer tries to use the compact layout for
///     datasets if the total data size is &lt; 64 kB and it should not be chunked.
/// </param>
/// <param name="IncludeStructFields">
///     A value that indicates whether struct fields are handled during serialization. The
///     default value is <see langword="true" />.
/// </param>
/// <param name="IncludeStructProperties">
///     A value that indicates whether struct properties are handled during
///     serialization. The default value is <see langword="false" />.
/// </param>
/// <param name="IncludeClassFields">
///     A value that indicates whether class fields are handled during serialization. The
///     default value is <see langword="false" />.
/// </param>
/// <param name="IncludeClassProperties">
///     A value that indicates whether class properties are handled during serialization.
///     The default value is <see langword="true" />.
/// </param>
/// <param name="FieldNameMapper">Maps a <see cref="FieldInfo" /> to the name of the HDF5 member.</param>
/// <param name="FieldStringLengthMapper">Maps a <see cref="FieldInfo" /> of type string to the desired string length.</param>
/// <param name="PropertyNameMapper">Maps a <see cref="PropertyInfo" /> to the name of the HDF5 member.</param>
/// <param name="PropertyStringLengthMapper">
///     Maps a <see cref="PropertyInfo" /> of type string to the desired string
///     length.
/// </param>
/// <param name="Filters">
///     A list of filters and their options to be applied to datasets that have no explicit filters
///     assigned.
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
    List<H5Filter>? Filters = default
)
{
    // Declared in the body rather than as further positional parameters, deliberately: adding a
    // parameter to a record's primary constructor changes its signature, which is a binary-breaking
    // change for anything compiled against the previous version, while adding a property is not.

    /// <summary>
    ///     How a string attribute is sized. The default, <see cref="H5AttributeStringLength.Measured" />, makes
    ///     it as wide as its own value.
    /// </summary>
    /// <remarks>
    ///     Independent of <see cref="DefaultStringLength" />, which continues to govern datasets and the string
    ///     members of a compound attribute. Set <see cref="H5AttributeStringLength.Inherit" /> to have
    ///     attributes follow it as well.
    /// </remarks>
    public H5AttributeStringLength AttributeStringLength { get; init; } = H5AttributeStringLength.Measured;

    /// <summary>
    ///     What to do with a string too long for the fixed-length width it is written into. The default,
    ///     <see cref="H5StringOverflow.Truncate" />, keeps the bytes that fit and discards the rest.
    /// </summary>
    /// <remarks>
    ///     Set <see cref="H5StringOverflow.Throw" /> to fail the write instead. Worth doing wherever declared
    ///     widths come from data that could grow, since the alternative is losing the excess with no
    ///     indication - and, because widths are in bytes, losing it mid-character.
    ///     <para>
    ///         Applies only where a width is DECLARED - a compound member sized by
    ///         <see cref="DefaultStringLength" /> or a string length mapper, or an attribute set to
    ///         <see cref="H5AttributeStringLength.Inherit" />. A measured width is taken from the value itself, so
    ///         it cannot overflow.
    ///     </para>
    /// </remarks>
    public H5StringOverflow StringOverflow { get; init; } = H5StringOverflow.Truncate;
}