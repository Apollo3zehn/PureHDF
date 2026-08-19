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

    // A caller may share one cache across concurrent reads by passing it via
    // H5DatasetAccess.ChunkCache, and _chunkInfoMap plus the ConsumedBytes accounting were otherwise
    // mutated with no synchronization. The default path never shares a cache - the default factory
    // builds one per read - so this only ever bit callers who opted in, and it bit them silently.
    //
    // A lock is affordable here because of what it guards: a miss costs a chunk read and usually
    // decompression, orders of magnitude more than the lock itself. It is deliberately NOT held
    // across chunkReader() - see GetChunk.
    private readonly object _lock = new();

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
    public int ConsumedSlots
    {
        get
        {
            // Reading Dictionary.Count while another reader mutates the dictionary is not safe, so
            // this observation is synchronized too - it is a diagnostic, never on the read path.
            lock (_lock)
            {
                return _chunkInfoMap.Count;
            }
        }
    }

    /// <summary>
    /// Gets the maximum size of the chunk cache in bytes.
    /// </summary>
    public ulong ByteCount { get; }

    /// <summary>
    /// Gets the number of consumed bytes of the chunk cache.
    /// </summary>
    public ulong ConsumedBytes { get; private set; }

    /// <inheritdoc />
    /// <remarks>
    ///     Safe to call concurrently. The lock is released around <paramref name="chunkReader" />:
    ///     holding it across a chunk read (I/O plus decompression) would serialize every reader
    ///     sharing this cache and so defeat the point of reading in parallel. The cost is that two
    ///     readers missing on the same chunk at the same time both decode it and one result is
    ///     discarded - wasted work, never incorrect.
    ///     <para>
    ///         Evicting a chunk another reader is still decoding from is likewise safe, but only
    ///         because cached chunks are plain GC-allocated arrays (see H5D_Chunk.ReadChunk and
    ///         H5Filter.ExecutePipeline): eviction drops a reference, and the holder's Memory keeps
    ///         the array alive. Pooling cached chunks would break that, and a lock would then no
    ///         longer be sufficient.
    ///     </para>
    /// </remarks>
    public Memory<byte> GetChunk(ulong chunkIndex, Func<Memory<byte>> chunkReader)
    {
        if (TryGetCachedChunk(chunkIndex, out var cached))
            return cached;

        return Install(chunkIndex, chunkReader());
    }

    /// <inheritdoc />
    /// <remarks>
    /// Shares all of its bookkeeping with <see cref="GetChunk" /> - the only difference is that the
    /// chunk reader is awaited rather than blocked on. The lock is not held across it either way, so
    /// nothing about the concurrency reasoning above changes.
    /// </remarks>
    public async ValueTask<Memory<byte>> GetChunkAsync(ulong chunkIndex, Func<ValueTask<Memory<byte>>> chunkReader)
    {
        if (TryGetCachedChunk(chunkIndex, out var cached))
            return cached;

        return Install(chunkIndex, await chunkReader().ConfigureAwait(false));
    }

    private bool TryGetCachedChunk(ulong chunkIndex, out Memory<byte> chunk)
    {
        lock (_lock)
        {
            if (_chunkInfoMap.TryGetValue(chunkIndex, out var cached))
            {
                cached.LastAccess = Environment.TickCount64;
                chunk = cached.Chunk;

                return true;
            }
        }

        chunk = default;

        return false;
    }

    private Memory<byte> Install(ulong chunkIndex, Memory<byte> buffer)
    {
        lock (_lock)
        {
            // Another reader may have installed this chunk while we were decoding it. Prefer the
            // installed one, so every reader observes the same buffer for a given index.
            if (_chunkInfoMap.TryGetValue(chunkIndex, out var installed))
            {
                installed.LastAccess = Environment.TickCount64;

                return installed.Chunk;
            }

            var chunkInfo = new ReadingChunkInfo(buffer) { LastAccess = Environment.TickCount64 };
            var chunk = chunkInfo.Chunk;

            if ((ulong)chunk.Length <= ByteCount)
            {
                // Nothing to preempt once the map is empty. Without that guard a cache constructed
                // with zero slots - which the constructor allows, and which reads as "do not cache" -
                // preempted an empty map and dereferenced the default KeyValuePair.
                while (_chunkInfoMap.Count > 0 &&
                      (_chunkInfoMap.Count >= ChunkSlotCount || ByteCount - ConsumedBytes < (ulong)chunk.Length))
                {
                    Preempt();
                }

                // Re-checked rather than assumed: with slots available the loop above has already made
                // room, but with none it exits on the emptiness guard and this chunk is not cacheable.
                if (_chunkInfoMap.Count < ChunkSlotCount)
                {
                    ConsumedBytes += (ulong)chunk.Length;
                    _chunkInfoMap[chunkIndex] = chunkInfo;
                }
            }

            return chunk;
        }
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