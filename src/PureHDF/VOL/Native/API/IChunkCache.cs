﻿namespace PureHDF.VOL.Native;

/// <summary>
/// Caches chunks during a read operation.
/// </summary>
public interface IChunkCache
{
    /// <summary>
    /// Tries to get the chunk at the given position.
    /// </summary>
    /// <param name="indices">The chunk position.</param>
    /// <param name="chunkReader">The chunk reader is used whenever the chunk is not already cached.</param>
    /// <param name="chunkWriter">The chunk writer is used whenever the chunk is evicted from the cache.</param>
    /// <returns>The chunk.</returns>
    public Task<Memory<byte>> GetChunkAsync(
        ulong[] indices, 
        Func<Task<Memory<byte>>> chunkReader, 
        Func<ulong[], Memory<byte>, Task>? chunkWriter = default);
}