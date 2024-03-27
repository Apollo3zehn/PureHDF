namespace PureHDF.VOL.Native;

public static partial class ChunkCache
{
    /// <summary>
    /// Gets or sets the default chunk cache factory for writing.
    /// </summary>
    public static Func<IWritingChunkCache> DefaultWritingChunkCacheFactory { get; set; } 
        = () => new SimpleWritingChunkCache();
}