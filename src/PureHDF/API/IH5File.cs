namespace PureHDF;

/// <summary>
/// An HDF5 file object. This is the entry-point to work with HDF5 files.
/// </summary>
public interface IH5File : IH5Group, IDisposable
{
    /// <summary>
    /// Gets the path of the opened HDF5 file if loaded from the file system.
    /// </summary>
    string? Path { get; }

    /// <summary>
    /// Gets or sets the current chunk cache factory.
    /// </summary>
    Func<IChunkCache> ChunkCacheFactory { get; set; }
}