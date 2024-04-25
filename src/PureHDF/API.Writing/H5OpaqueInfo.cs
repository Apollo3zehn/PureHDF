namespace PureHDF;

/// <summary>
/// Opaque type info.
/// </summary>
/// <param name="TypeSize">The size of the opaque type.</param>
/// <param name="Tag">The ASCII tag to be used for opaque types.</param>
public record H5OpaqueInfo(uint TypeSize, string Tag);