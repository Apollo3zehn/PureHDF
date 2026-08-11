using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace PureHDF.VOL.Native;

/// <summary>
/// A thread-safe, size-bounded cache of decoded structures keyed by file address, with
/// approximate-LRU eviction.
/// </summary>
/// <remarks>
/// This exists for the caches in the reader whose size is proportional to DATA VOLUME rather than to
/// the shape of the file. Most caches here are not: object headers, local heaps, fractal heap headers
/// and b-tree headers are all bounded by how many groups, attributes and datasets a file contains.
/// Two kinds are not, and both use this.
/// <para>
/// B-TREE NODES. A chunk index has a node per group of chunks, so a process that holds a file open and
/// services selective reads over time converged on retaining the entire index: measured at ~34 bytes
/// per chunk touched, with no eviction and no upper bound. Simply not caching the nodes is not the
/// alternative - measured on a repeated full read of a chunked dataset, dropping the leaf-node cache
/// recovered 98% of the memory but gave back 92% of the read saving (0 -> 132 reads against a 144-read
/// baseline), because the repetition that matters is WITHIN a single read: one index lookup per chunk,
/// many chunks per leaf. The memory and the saving are close to the same thing, so the answer is a
/// bound rather than a switch.
/// </para>
/// <para>
/// GLOBAL HEAP COLLECTIONS. These hold decoded variable-length PAYLOAD, so retention grew with how
/// much variable-length data had been read and was released only when the file was closed. Not caching
/// them is even less of an option than for nodes: decoding a collection decodes every object in it, and
/// a variable-length read resolves many elements out of the same collection, so a miss is expensive
/// and hits are the normal case.
/// </para>
/// <para>
/// Entries carry a COST, and the bound is a budget of that cost. That is what lets one mechanism serve
/// both. Nodes are all roughly one node in size, so counting them bounds memory and they pass no cost
/// at all. Collections cannot be counted: a collection is at least 4 KiB but has no upper size, because
/// a single large variable-length value gets a collection to itself - so counting collections would
/// bound nothing at all. They pass their size and get a byte budget.
/// </para>
/// <para>
/// Hits are lock-free: a hit touches the entry's stamp with one interlocked increment and nothing
/// else, because concurrent lookups against one shared cached b-tree are exactly what the
/// per-operation driver work made possible and a lock on this path would serialize them again.
/// Eviction takes a lock, runs only on insert, and trims a BATCH so that its cost is amortized over
/// many inserts instead of a scan per insert - which matters because the pathological case is a
/// sequential sweep over millions of chunks, where every lookup misses.
/// </para>
/// <para>
/// EVICTION IS SAFE while a caller still holds what was evicted, and that is a real obligation rather
/// than an assumption. Everything cached here is either immutable (a node record) or backed by
/// plain GC-allocated arrays (a collection's object data, straight from <c>ReadBytes</c>): eviction
/// drops a reference and the holder's own reference keeps the array alive. Pooling any of it would
/// break that.
/// </para>
/// </remarks>
internal sealed class BoundedAddressCache<T> where T : notnull
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

    // The default budget for a byte-costed cache, i.e. for global heap collections.
    //
    // Deliberately generous, and the number is measured rather than picked. A first attempt used 1 MiB
    // (matching SimpleReadingChunkCache) on the theory that a variable-length read only needs the few
    // collections its elements point into. That is true of ONE pass; it is false of repeated passes,
    // which is the access pattern that matters. A benchmark doing repeated full reads of a 2.4 MiB
    // variable-length dataset went from 178 us to 3,005 us - 17x - because each pass evicted the
    // collections the next pass would start on, turning a fully warm read into a fully cold one.
    // Collections hold payload, so re-decoding one is expensive in a way that re-decoding a b-tree node
    // is not.
    //
    // 64 MiB keeps any realistic single-dataset working set resident while still bounding the case this
    // exists for: a long-lived reader that walks gigabytes of variable-length data used to retain all of
    // it until the file was closed. Note the scope - the budget is per OPEN FILE, so a process holding
    // many files open should lower it via H5ReadOptions.
    public const long DefaultByteBudget = 64 * 1024 * 1024;

    private sealed class Entry(T value, long cost)
    {
        public T Value { get; } = value;

        public long Cost { get; } = cost;

        public long LastAccess;
    }

    private readonly ConcurrentDictionary<ulong, Entry> _entries = new();
    private readonly object _evictionLock = new();
    private readonly long _budget;

    private long _clock;
    private long _consumed;

    public BoundedAddressCache(long budget = DefaultCapacity)
    {
        if (budget <= 0)
            throw new ArgumentOutOfRangeException(nameof(budget), "The budget must be positive.");

        _budget = budget;
    }

    public bool TryGetValue(ulong address, [MaybeNullWhen(false)] out T value)
    {
        if (_entries.TryGetValue(address, out var entry))
        {
            Touch(entry);
            value = entry.Value;

            return true;
        }

        value = default;

        return false;
    }

    /// <summary>
    /// Installs <paramref name="value" /> unless another caller got there first, and returns whichever
    /// instance is now cached - so that everyone shares one instance for a given address.
    /// </summary>
    /// <param name="address">The file address this entry is keyed by.</param>
    /// <param name="value">The decoded structure to cache.</param>
    /// <param name="cost">
    /// What this entry consumes of the budget. Leave at 1 to bound by entry count.
    /// </param>
    /// <remarks>
    /// Uses TryAdd rather than GetOrAdd so that the budget is charged by - and only by - whoever
    /// actually installed the entry. Charging on every call would double-count an address two readers
    /// decoded at once, and for a value type there is no way to tell "mine was installed" from
    /// "an equal one was" after the fact.
    /// </remarks>
    public T GetOrAdd(ulong address, T value, long cost = 1)
    {
        var entry = new Entry(value, cost);

        if (_entries.TryAdd(address, entry))
        {
            Touch(entry);

            if (Interlocked.Add(ref _consumed, cost) > _budget)
                Evict();

            return value;
        }

        // Another caller installed this address first - share theirs, so everyone reading this address
        // observes one instance.
        if (_entries.TryGetValue(address, out var installed))
        {
            Touch(installed);

            return installed.Value;
        }

        // Installed and then evicted again between those two calls. Vanishingly rare, and harmless:
        // the caller gets a correct value that simply is not cached.
        return value;
    }

    // How far down the recency order an entry may already be before a hit bothers to re-stamp it.
    // Sized well under the smallest useful entry count, so an entry inside the window is unambiguously
    // still hot and skipping its stamp cannot promote a cold entry.
    private const long TouchWindow = 32;

    // A monotonic counter rather than Environment.TickCount64 (which SimpleReadingChunkCache uses):
    // at ~15 ms resolution a burst of lookups would stamp many entries identically and the eviction
    // order would degenerate to arbitrary, which is worst for exactly the sweep this bound is for.
    //
    // Re-stamping is SKIPPED for an entry that is already among the most recently used. A
    // variable-length read resolves many elements out of the same few collections, so hits on one hot
    // entry dominate, and making each of them pay an interlocked increment measured ~6-10% on a
    // hit-dominated read. An entry inside the window is already at the recent end of the eviction order,
    // so re-stamping it changes nothing about what gets evicted.
    private void Touch(Entry entry)
    {
        var clock = Volatile.Read(ref _clock);

        if (clock - Volatile.Read(ref entry.LastAccess) < TouchWindow)
            return;

        Volatile.Write(ref entry.LastAccess, Interlocked.Increment(ref _clock));
    }

    private void Evict()
    {
        // Soft cap. If another thread is already trimming, this insert simply proceeds - the budget
        // overshoots briefly rather than every inserting thread queueing behind one scan.
        if (!Monitor.TryEnter(_evictionLock))
            return;

        try
        {
            // Trim to three quarters of the budget rather than exactly to it, so a scan happens once
            // per batch of inserts instead of on every insert past the cap.
            var target = _budget - (_budget / 4);

            if (Volatile.Read(ref _consumed) <= target)
                return;

            var victims = _entries
                .OrderBy(pair => Volatile.Read(ref pair.Value.LastAccess))
                .ToArray();

            // Stops one short of the end, so the most recently used entry is never evicted. Without
            // this, an entry whose own cost exceeds the budget was dropped by the very insert that
            // added it - so it could never be cached at all, and every lookup re-decoded it. A single
            // large variable-length value gets a global heap collection to itself, which is exactly
            // that case.
            for (int i = 0; i < victims.Length - 1; i++)
            {
                if (Volatile.Read(ref _consumed) <= target)
                    break;

                if (_entries.TryRemove(victims[i].Key, out var removed))
                    Interlocked.Add(ref _consumed, -removed.Cost);
            }
        }

        finally
        {
            Monitor.Exit(_evictionLock);
        }
    }
}
