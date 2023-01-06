namespace HDF5.NET
{
    /// <summary>
    /// Caches chunks during a read operation.
    /// </summary>
    public interface IChunkCache
    {
        /// <summary>
        /// Tries to get the chunk at the given position.
        /// </summary>
        /// <param name="indices">The chunk position.</param>
        /// <param name="chunkLoader">The chunk load is used whenever the chunk is not already cached.</param>
        /// <returns>The chunk.</returns>
        public Task<Memory<byte>> GetChunkAsync(ulong[] indices, Func<Task<Memory<byte>>> chunkLoader);
    }
}
