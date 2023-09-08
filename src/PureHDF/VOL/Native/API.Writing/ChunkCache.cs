namespace PureHDF.VOL.Native;

public static partial class ChunkCache
{
    /// <summary>
    /// The default chunk cache factory for writing.
    /// </summary>
    public static Func<IWritingChunkCache> DefaultWritingChunkCacheFactory { get; } = () => new SimpleChunkCache();
}