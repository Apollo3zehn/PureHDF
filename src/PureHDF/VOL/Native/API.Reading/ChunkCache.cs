namespace PureHDF.VOL.Native;

/// <summary>
/// A static class to provide a default value for chunk cache factories.
/// </summary>
public static partial class ChunkCache
{
    /// <summary>
    /// The default chunk cache factory for reading.
    /// </summary>
    public static Func<IReadingChunkCache> DefaultReadingChunkCacheFactory { get; } = () => new SimpleChunkCache();
}