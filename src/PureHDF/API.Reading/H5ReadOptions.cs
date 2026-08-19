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
/// <param name="GlobalHeapCacheByteBudget">
/// A value that indicates how many bytes of decoded variable-length data may be cached per open file.
/// The default value is 64 MiB.
/// <para>
/// This bounds the only read cache whose footprint grows with how much data has been read rather than
/// with the shape of the file, because it holds decoded payload rather than file structure. Raising it
/// speeds up repeated reads of the same variable-length data; lowering it caps memory, which matters for
/// a process holding many files open at once, since the budget applies PER FILE. Below the working set
/// of a single read pass, repeated reads degrade to re-decoding everything.
/// </para>
/// </param>
public record H5ReadOptions(
    bool IncludeStructFields = true,
    bool IncludeStructProperties = false,
    bool IncludeClassFields = false,
    bool IncludeClassProperties = true,
    Func<FieldInfo, string?>? FieldNameMapper = default,
    Func<PropertyInfo, string?>? PropertyNameMapper = default,
    long GlobalHeapCacheByteBudget = 64 * 1024 * 1024
);