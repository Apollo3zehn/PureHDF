using System;

namespace HDF5.NET
{
    public interface IChunkCache
    {
        public Memory<byte> GetChunk(ulong[] indices, Func<Memory<byte>> chunkLoader);
    }
}
