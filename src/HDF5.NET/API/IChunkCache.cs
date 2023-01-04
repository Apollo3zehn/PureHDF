namespace HDF5.NET
{
    public interface IChunkCache
    {
        public Task<Memory<byte>> GetChunkAsync(ulong[] indices, Func<Task<Memory<byte>>> chunkLoader);
    }
}
