namespace PureHDF.VOL.Native;

/// <summary>
/// A static class to provide a default value for chunk cache factories.
/// </summary>
public static partial class ChunkCache
{
    /// <summary>
    /// Gets or sets the default chunk cache factory for reading.
    /// </summary>
    public static Func<IReadingChunkCache> DefaultReadingChunkCacheFactory { get; set; } 
        = () => new SimpleReadingChunkCache();
}