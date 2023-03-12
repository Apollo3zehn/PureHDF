namespace PureHDF;

/// <inheritdoc />
public interface IH5NativeFile : IH5File
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