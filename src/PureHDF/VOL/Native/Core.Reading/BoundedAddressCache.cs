using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace PureHDF.VOL.Native;

/// <summary>
/// A thread-safe, size-bounded cache of decoded structures keyed by file address, with
/// approximate-LRU eviction.
/// </summary>
/// <remarks>
/// This exists because the b-tree node caches are the only caches in the reader whose size is
/// proportional to DATA VOLUME rather than to the shape of the file. Everything else - object headers,
/// local heaps, fractal heap headers, b-tree headers - is bounded by how many groups, attributes and
/// datasets a file contains. A chunk index is bounded by the chunk count, so a process that holds a
/// file open and services selective reads over time converged on retaining the entire index: measured
/// at ~34 bytes per chunk touched, with no eviction and no upper bound.
/// <para>
/// Simply not caching the nodes is not the alternative. Measured on a repeated full read of a chunked
/// dataset, dropping the leaf-node cache recovered 98% of the memory but gave back 92% of the read
/// saving (0 -> 132 reads against a 144-read baseline), because the repetition that matters is WITHIN
/// a single read - one index lookup per chunk, many chunks per leaf - and not only between reads. The
/// memory and the saving are close to the same thing, so the answer is a bound rather than a switch.
/// </para>
/// <para>
/// Hits are lock-free: a hit touches the entry's stamp with one interlocked increment and nothing
/// else, because concurrent lookups against one shared cached b-tree are exactly what the
/// per-operation driver work made possible and a lock on this path would serialize them again.
/// Eviction takes a lock, runs only on insert, and trims a BATCH so that its cost is amortized over
/// many inserts instead of a scan per insert - which matters because the pathological case is a
/// sequential sweep over millions of chunks, where every lookup misses.
/// </para>
/// </remarks>
internal sealed class BoundedAddressCache<T> where T : class
{
    // Counted in NODES, not records: a b-tree leaf holds many records, so the memory this bounds is
    // (capacity x node size) and not (capacity x record size). Measured at ~3.1 KiB per leaf for a
    // rank-2 chunk index, so this cap is on the order of 800 KiB per b-tree - flat whether the dataset
    // has ten thousand chunks or ten million.
    //
    // Chosen to be comfortably larger than any plausible working set of a repeated-region read (the
    // access pattern this cache exists for) while small enough that the worst case is uninteresting.
    // A full-file sweep now degrades to the uncached read count instead of retaining the whole index.
    public const int DefaultCapacity = 256;

    private sealed class Entry(T value)
    {
        public T Value { get; } = value;

        public long LastAccess;
    }

    private readonly ConcurrentDictionary<ulong, Entry> _entries = new();
    private readonly object _evictionLock = new();
    private readonly int _capacity;

    private long _clock;

    public BoundedAddressCache(int capacity = DefaultCapacity)
    {
        if (capacity <= 0)
            throw new ArgumentOutOfRangeException(nameof(capacity), "The capacity must be positive.");

        _capacity = capacity;
    }

    public bool TryGetValue(ulong address, [NotNullWhen(true)] out T? value)
    {
        if (_entries.TryGetValue(address, out var entry))
        {
            Touch(entry);
            value = entry.Value;

            return true;
        }

        value = null;

        return false;
    }

    /// <summary>
    /// Installs <paramref name="value" /> unless another caller got there first, and returns whichever
    /// instance is now cached - so that everyone shares one instance for a given address.
    /// </summary>
    public T GetOrAdd(ulong address, T value)
    {
        var entry = _entries.GetOrAdd(address, static (_, v) => new Entry(v), value);

        Touch(entry);

        if (_entries.Count > _capacity)
            Evict();

        return entry.Value;
    }

    // A monotonic counter rather than Environment.TickCount64 (which SimpleReadingChunkCache uses):
    // at ~15 ms resolution a burst of lookups would stamp many entries identically and the eviction
    // order would degenerate to arbitrary, which is worst for exactly the sweep this bound is for.
    private void Touch(Entry entry)
    {
        Volatile.Write(ref entry.LastAccess, Interlocked.Increment(ref _clock));
    }

    private void Evict()
    {
        // Soft cap. If another thread is already trimming, this insert simply proceeds - the count
        // overshoots briefly rather than every inserting thread queueing behind one scan.
        if (!Monitor.TryEnter(_evictionLock))
            return;

        try
        {
            var excess = _entries.Count - _capacity;

            if (excess <= 0)
                return;

            // Trim a quarter of the cache beyond the excess, so a scan happens once per ~capacity/4
            // inserts rather than on every insert past the cap.
            var victimCount = excess + _capacity / 4;

            var victims = _entries
                .OrderBy(pair => Volatile.Read(ref pair.Value.LastAccess))
                .Take(victimCount)
                .Select(pair => pair.Key)
                .ToArray();

            foreach (var address in victims)
            {
                _entries.TryRemove(address, out _);
            }
        }

        finally
        {
            Monitor.Exit(_evictionLock);
        }
    }
}
