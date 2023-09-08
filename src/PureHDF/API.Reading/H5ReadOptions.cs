using System.Reflection;

namespace PureHDF;

/// <summary>
/// Provides options to be used with <see cref="H5File"/>.
/// </summary>
/// <param name="IncludeStructFields">A value that indicates whether struct fields are handled during serialization. The default value is <see langword="true"/>.</param>
/// <param name="IncludeStructProperties">A value that indicates whether struct properties are handled during serialization. The default value is <see langword="false"/>.</param>
/// <param name="IncludeClassFields">A value that indicates whether class fields are handled during serialization. The default value is <see langword="false"/>.</param>
/// <param name="IncludeClassProperties">A value that indicates whether class properties are handled during serialization. The default value is <see langword="true"/>.</param>
/// <param name="FieldNameMapper">Maps a <see cref="FieldInfo"/> to the name of the HDF5 member.</param>
/// <param name="PropertyNameMapper">Maps a <see cref="PropertyInfo"/> to the name of the HDF5 member.</param>
public record H5ReadOptions(
    bool IncludeStructFields = true,
    bool IncludeStructProperties = false,
    bool IncludeClassFields = false,
    bool IncludeClassProperties = true,
    Func<FieldInfo, string?>? FieldNameMapper = default,
    Func<PropertyInfo, string?>? PropertyNameMapper = default
);