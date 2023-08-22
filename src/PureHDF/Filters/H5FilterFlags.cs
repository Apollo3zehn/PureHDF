namespace PureHDF.Filters;

/// <summary>
/// Specifies the filter flags.
/// </summary>
public enum H5FilterFlags : ushort
{
    /// <summary>
    /// No filter flags.
    /// </summary>
    None = 0,

    /// <summary>
    /// A flag which indicates if the provided buffer should be decompressed.
    /// </summary>
    Decompress = 0x0100,

    /// <summary>
    /// A flag which indicates if EDC filters should be skipped.
    /// </summary>
    SkipEdc = 0x0200
}