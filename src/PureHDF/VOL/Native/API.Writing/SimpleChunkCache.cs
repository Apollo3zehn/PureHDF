namespace PureHDF.VOL.Native;

// https://support.hdfgroup.org/HDF5/doc/H5.user/Caching.html
// https://support.hdfgroup.org/HDF5/faq/perfissues.html

/// <summary>
/// A simple chunk cache.
/// </summary>
public partial class SimpleChunkCache : IWritingChunkCache
{
    /// <inheritdoc />
    public Memory<byte> GetChunk(
        ulong[] indices, 
        Func<Memory<byte>> chunkAllocator, 
        Action<ulong[], Memory<byte>> chunkWriter)
    {
        if (_chunkInfoMap.TryGetValue(indices, out var chunkInfo))
        {
            chunkInfo.LastAccess = DateTime.Now;
        }

        else
        {
            var buffer = chunkAllocator();
            chunkInfo = new ChunkInfo(buffer) { LastAccess = DateTime.Now };
            var chunk = chunkInfo.Chunk;

            if ((ulong)chunk.Length <= ByteCount)
            {
                while (_chunkInfoMap.Count >= ChunkSlotCount || ByteCount - ConsumedBytes < (ulong)chunk.Length)
                {
                    Preempt(chunkWriter);
                }

                ConsumedBytes += (ulong)chunk.Length;
                _chunkInfoMap[indices] = chunkInfo;
            }
        }

        return chunkInfo.Chunk;
    }

    /// <inheritdoc />
    public void Flush(Action<ulong[], Memory<byte>>? chunkWriter = null)
    {
        foreach (var entry in _chunkInfoMap)
        {
            if (chunkWriter is not null)
                chunkWriter(entry.Key, entry.Value.Chunk);
        }

        _chunkInfoMap.Clear();
        ConsumedBytes = 0;
    }
}