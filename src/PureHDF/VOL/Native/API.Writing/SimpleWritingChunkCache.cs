namespace PureHDF.VOL.Native;

/// <summary>
/// A simple writing chunk cache which has not limits in size.
/// </summary>
public partial class SimpleWritingChunkCache : IWritingChunkCache
{
    private record WritingChunkInfo(Memory<byte> Chunk);

    private readonly Dictionary<ulong[], WritingChunkInfo> _chunkInfoMap;

    internal SimpleWritingChunkCache()
    {
        _chunkInfoMap = new Dictionary<ulong[], WritingChunkInfo>(new ArrayEqualityComparer());
    }

    /// <inheritdoc />
    public Memory<byte> GetChunk(
        ulong[] indices,
        Func<Memory<byte>> chunkAllocator,
        Action<ulong[], Memory<byte>> chunkWriter)
    {
        if (_chunkInfoMap.TryGetValue(indices, out var chunkInfo))
        {
            //
        }

        else
        {
            var buffer = chunkAllocator();
            chunkInfo = new WritingChunkInfo(buffer);
            _chunkInfoMap[indices] = chunkInfo;
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
    }

    // https://stackoverflow.com/questions/14663168/an-integer-array-as-a-key-for-dictionary
    private class ArrayEqualityComparer : IEqualityComparer<ulong[]>
    {
        public bool Equals(ulong[]? x, ulong[]? y)
        {
            if (x is null || y is null)
                return x is null && y is null;

            if (x.Length != y.Length)
                return false;

            for (int i = 0; i < x.Length; i++)
            {
                if (x[i] != y[i])
                {
                    return false;
                }
            }

            return true;
        }

        public int GetHashCode(ulong[] obj)
        {
            int result = 17;

            for (int i = 0; i < obj.Length; i++)
            {
                unchecked
                {
                    result = result * 23 + unchecked((int)obj[i]);
                }
            }

            return result;
        }
    }
}