namespace PureHDF.VOL.Native;

// https://support.hdfgroup.org/HDF5/doc/H5.user/Caching.html
// https://support.hdfgroup.org/HDF5/faq/perfissues.html


/// <summary>
/// A simple chunk cache.
/// </summary>
public class SimpleChunkCache : IChunkCache
{
    #region Records

    private record ChunkInfo(Memory<byte> Chunk)
    {
        public DateTime LastAccess { get; set; }
    }

    #endregion

    #region Fields

    private readonly Dictionary<ulong[], ChunkInfo> _chunkInfoMap;

    #endregion

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="SimpleChunkCache"/> class.
    /// </summary>
    /// <param name="chunkSlotCount">The number of chunks that can be hold in the cache at the same time.</param>
    /// <param name="byteCount">The maximum size of the chunk cache in bytes.</param>
    public SimpleChunkCache(int chunkSlotCount = 521, ulong byteCount = 1 * 1024 * 1024/*, double w0 = 0.75*/)
    {
        if (chunkSlotCount < 0)
            throw new Exception("The chunk slot count parameter must be >= 0.");

        //if (!(0 <= w0 && w0 <= 1))
        //    throw new ArgumentException("The parameter w0 must be in the range of 0..1 (inclusive).");

        ChunkSlotCount = chunkSlotCount;
        ByteCount = byteCount;

        _chunkInfoMap = new Dictionary<ulong[], ChunkInfo>(new ArrayEqualityComparer());
    }

    #endregion

    #region Properties

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

    #endregion

    #region Methods

    /// <inheritdoc />
    public async Task<Memory<byte>> GetChunkAsync(
        ulong[] indices, 
        Func<Task<Memory<byte>>> chunkReader, 
        Func<ulong[], Memory<byte>, Task>? chunkWriter = default)
    {
        if (_chunkInfoMap.TryGetValue(indices, out var chunkInfo))
        {
            chunkInfo.LastAccess = DateTime.Now;
        }

        else
        {
            var buffer = await chunkReader().ConfigureAwait(false);
            chunkInfo = new ChunkInfo(buffer) { LastAccess = DateTime.Now };
            var chunk = chunkInfo.Chunk;

            if ((ulong)chunk.Length <= ByteCount)
            {
                while (_chunkInfoMap.Count >= ChunkSlotCount || ByteCount - ConsumedBytes < (ulong)chunk.Length)
                {
                    await PreemptAsync(chunkWriter);
                }

                ConsumedBytes += (ulong)chunk.Length;
                _chunkInfoMap[indices] = chunkInfo;
            }
        }

        return chunkInfo.Chunk;
    }

    private async Task PreemptAsync(Func<ulong[], Memory<byte>, Task>? chunkWriter)
    {
        var entry = _chunkInfoMap
            .OrderBy(current => current.Value.LastAccess)
            .FirstOrDefault();

        if (chunkWriter is not null)
            await chunkWriter(entry.Key, entry.Value.Chunk);

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

    #endregion
}