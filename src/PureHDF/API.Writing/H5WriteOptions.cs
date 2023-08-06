using System.Reflection;

namespace PureHDF;

/// <summary>
/// Provides options to be used with <see cref="H5File"/>.
/// </summary>
/// <param name="DefaultStringLength">Gets a value that indicates how strings are handled during serialization. A nonzero positve value means that strings are treated as fixed-length strings, otherwise they are variable-length strings. The default value is 0.</param>
/// <param name="GlobalHeapCollectionSize">Gets a value that indicates the size of a global heap collection in bytes. The default value is 4096 bytes which is the minimum allowed size.</param>
/// <param name="GlobalHeapFlushThreshold">Gets a value that indicates the threshold after which global heap collections will be flushed. The default value is 4096 * 1024 = 4 MB.</param>
/// <param name="PreferCompactDatasetLayout">Gets a value that indicates whether the writer tries to use the compact layout for datasets if the total data size is &lt; 64 kB and it should not be chunked.</param>
/// <param name="IncludeStructFields">Gets a value that indicates whether struct fields are handled during serialization. The default value is <see langword="true"/>.</param>
/// <param name="IncludeStructProperties">Gets a value that indicates whether struct properties are handled during serialization. The default value is <see langword="false"/>.</param>
/// <param name="IncludeClassFields">Gets a value that indicates whether class fields are handled during serialization. The default value is <see langword="false"/>.</param>
/// <param name="IncludeClassProperties">Gets a value that indicates whether class properties are handled during serialization. The default value is <see langword="true"/>.</param>
/// <param name="FieldNameMapper">Maps a <see cref="FieldInfo"/> to the name of the HDF5 member.</param>
/// <param name="FieldStringLengthMapper">Maps a <see cref="FieldInfo"/> of type string to the desired string length.</param>
/// <param name="PropertyNameMapper">Maps a <see cref="PropertyInfo"/> to the name of the HDF5 member.</param>
/// <param name="PropertyStringLengthMapper">Maps a <see cref="PropertyInfo"/> of type string to the desired string length.</param>
public record H5WriteOptions(
    int DefaultStringLength = default,
    int GlobalHeapCollectionSize = 4096,
    long GlobalHeapFlushThreshold = 4096 * 1024,
    bool PreferCompactDatasetLayout = true,
    bool IncludeStructFields = true,
    bool IncludeStructProperties = false,
    bool IncludeClassFields = false,
    bool IncludeClassProperties = true,
    Func<FieldInfo, string?>? FieldNameMapper = default,
    Func<FieldInfo, int?>? FieldStringLengthMapper = default,
    Func<PropertyInfo, string?>? PropertyNameMapper = default,
    Func<PropertyInfo, int?>? PropertyStringLengthMapper = default
);