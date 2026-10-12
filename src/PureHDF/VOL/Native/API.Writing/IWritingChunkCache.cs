namespace PureHDF.VOL.Native;

/// <summary>
/// Caches chunks during write operations.
/// </summary>
public interface IWritingChunkCache
{
    /// <summary>
    /// Tries to get the chunk at the given index.
    /// </summary>
    /// <param name="chunkIndex">The linear chunk index.</param>
    /// <param name="chunkAllocator">The chunk allocator is used whenever the chunk is not already cached.</param>
    /// <param name="chunkWriter">The chunk writer is used whenever the chunk is flushed from the cache.</param>
    public Memory<byte> GetChunk(
        ulong chunkIndex,
        Func<Memory<byte>> chunkAllocator,
        Action<ulong, Memory<byte>> chunkWriter);

    /// <summary>
    /// Removes a chunk from the cache without writing it, so the caller can write it now.
    /// </summary>
    /// <param name="chunkIndex">The linear chunk index.</param>
    /// <param name="chunk">The removed chunk, if it was cached.</param>
    /// <returns>True if the chunk was cached and has been removed.</returns>
    /// <remarks>
    /// Lets a chunk be written as soon as all of its elements have arrived, rather than when the cache is
    /// flushed - without it, a chunked dataset written in batches holds all of its data until the writer
    /// is disposed. The default returns false, which keeps that behavior for a cache that does not
    /// implement it.
    /// </remarks>
    public bool TryRemove(ulong chunkIndex, out Memory<byte> chunk)
    {
        chunk = default;
        return false;
    }

    /// <summary>
    /// Flushes the chunk.
    /// </summary>
    /// <param name="chunkWriter">The chunk writer used for chunks being flushed from the cache.</param>
    public void Flush(Action<ulong, Memory<byte>> chunkWriter);
}