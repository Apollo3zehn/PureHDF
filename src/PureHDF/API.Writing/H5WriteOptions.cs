using System.Reflection;

namespace PureHDF;

/// <summary>
/// Provides options to be used with <see cref="H5File"/>.
/// </summary>
/// <param name="DefaultStringLength">A value that indicates how strings are handled during serialization. A nonzero positve value means that strings are treated as fixed-length strings, otherwise they are variable-length strings. The default value is 0.</param>
/// <param name="MinimumGlobalHeapCollectionSize">A value that indicates the minimum size of a global heap collection in bytes. The default value is 4096 bytes which is the absolute minimum allowed size.</param>
/// <param name="GlobalHeapFlushThreshold">A value that indicates the threshold after which global heap collections will be flushed. The default value is 4096 * 1024 = 4 MB.</param>
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
);