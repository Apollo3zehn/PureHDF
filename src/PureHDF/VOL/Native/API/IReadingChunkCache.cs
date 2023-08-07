namespace PureHDF.VOL.Native;

/// <summary>
/// Caches chunks during read operations.
/// </summary>
public interface IReadingChunkCache
{
    /// <summary>
    /// Tries to get the chunk at the given position.
    /// </summary>
    /// <param name="indices">The chunk position.</param>
    /// <param name="chunkReader">The chunk reader is used whenever the chunk is not already cached.</param>
    public Task<Memory<byte>> GetChunkAsync(
        ulong[] indices, 
        Func<Task<Memory<byte>>> chunkReader);
}