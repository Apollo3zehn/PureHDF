namespace PureHDF.VOL.Native;

// https://support.hdfgroup.org/HDF5/doc/H5.user/Caching.html
// https://support.hdfgroup.org/HDF5/faq/perfissues.html


/// <summary>
/// A simple reading chunk cache following the cache design principles of the HDF5 C-library.
/// </summary>
public class SimpleReadingChunkCache : IReadingChunkCache
{
    private record ReadingChunkInfo(Memory<byte> Chunk)
    {
        public long LastAccess { get; set; }
    }

    private readonly Dictionary<ulong, ReadingChunkInfo> _chunkInfoMap = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="SimpleReadingChunkCache"/> class.
    /// </summary>
    /// <param name="chunkSlotCount">The number of chunks that can be hold in the cache at the same time.</param>
    /// <param name="byteCount">The maximum size of the chunk cache in bytes.</param>
    public SimpleReadingChunkCache(int chunkSlotCount = 521, ulong byteCount = 1 * 1024 * 1024/*, double w0 = 0.75*/)
    {
        if (chunkSlotCount < 0)
            throw new Exception("The chunk slot count parameter must be >= 0.");

        //if (!(0 <= w0 && w0 <= 1))
        //    throw new ArgumentException("The parameter w0 must be in the range of 0..1 (inclusive).");

        ChunkSlotCount = chunkSlotCount;
        ByteCount = byteCount;
    }

    /// <summary>
    /// Gets the number of chunks that can be hold in the cache at the same time.
    /// </summary>
    public int ChunkSlotCount { get; }

    /// <summary>
    /// Gets the number of chunk slots that have already been consumed.
    /// </summary>
    public int ConsumedSlots => _chunkInfoMap.Count;

    /// <summary>
    /// Gets the maximum size of the chunk cache in bytes.
    /// </summary>
    public ulong ByteCount { get; }

    /// <summary>
    /// Gets the number of consumed bytes of the chunk cache.
    /// </summary>
    public ulong ConsumedBytes { get; private set; }

    /// <inheritdoc />
    public Memory<byte> GetChunk(ulong chunkIndex, Func<Memory<byte>> chunkReader)
    {
        if (_chunkInfoMap.TryGetValue(chunkIndex, out var chunkInfo))
        {
#if NET6_0_OR_GREATER
            chunkInfo.LastAccess = Environment.TickCount64;
#else
            chunkInfo.LastAccess = Environment.TickCount;
#endif
        }

        else
        {
            var buffer = chunkReader();
#if NET6_0_OR_GREATER
            chunkInfo = new ReadingChunkInfo(buffer) { LastAccess = Environment.TickCount64 };
#else
            chunkInfo = new ReadingChunkInfo(buffer) { LastAccess = Environment.TickCount };
#endif
            var chunk = chunkInfo.Chunk;

            if ((ulong)chunk.Length <= ByteCount)
            {
                while (_chunkInfoMap.Count >= ChunkSlotCount || ByteCount - ConsumedBytes < (ulong)chunk.Length)
                {
                    Preempt();
                }

                ConsumedBytes += (ulong)chunk.Length;
                _chunkInfoMap[chunkIndex] = chunkInfo;
            }
        }

        return chunkInfo.Chunk;
    }

    private void Preempt()
    {
        var entry = _chunkInfoMap
            .OrderBy(current => current.Value.LastAccess)
            .FirstOrDefault();

        ConsumedBytes -= (ulong)entry.Value.Chunk.Length;
        _chunkInfoMap.Remove(entry.Key);
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