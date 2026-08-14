using System.Collections.Concurrent;

namespace PureHDF.VOL.Native;

internal static class NativeCache
{
    #region Constructors

    static NativeCache()
    {
        _globalHeapMap = new ConcurrentDictionary<NativeCacheToken, BoundedAddressCache<GlobalHeapCollection>>();
        _fileMap = new ConcurrentDictionary<NativeCacheToken, ConcurrentDictionary<string, NativeFile>>();
        _structureMap = new ConcurrentDictionary<NativeCacheToken, ConcurrentDictionary<(Type, ulong), object>>();
    }

    #endregion

    #region Shared

    public static void Clear(NativeReadContext context)
    {
        // global heap
        _globalHeapMap.TryRemove(context.CacheToken, out var _);

        // structures
        _structureMap.TryRemove(context.CacheToken, out var _);

        // file map
        if (_fileMap.TryRemove(context.CacheToken, out var pathToNativeFileMap))
        {
            foreach (var nativeFile in pathToNativeFileMap.Values)
            {
                nativeFile.Dispose();
            }
        }
    }

    #endregion

    #region Structures

    // Decoded structural objects - local heaps, b-tree headers, fractal heap headers - keyed per file
    // by the address they were decoded from.
    //
    // This exists because the retained object-header messages (SymbolTableMessage, LinkInfoMessage,
    // AttributeInfoMessage, ...) may not cache these themselves. They outlive the read operation that
    // decoded them, so anything they hold must be free of a per-operation driver; upstream cached
    // them in unsynchronised lazy fields holding a captured context, which is exactly the
    // cursor-corruption class the per-operation driver exists to prevent, so those caches were
    // removed and every by-name lookup re-decoded the storage it had already walked.
    //
    // Keying here rather than on the message restores the caching without touching the messages:
    // entries are per FILE and per ADDRESS, so two messages pointing at the same heap share one
    // decode, the cache dies with the file, and record equality of the messages stays a function of
    // the file bytes rather than of which lookups happened to run first.
    //
    // The cached values must be immutable or internally thread-safe, since concurrent operations on
    // one file share them. That is a real obligation on the value types, not an assumption: LocalHeap
    // is immutable, and the b-tree/fractal-heap headers hold their own ConcurrentDictionary node
    // caches whose contents are immutable records.
    private static readonly ConcurrentDictionary<NativeCacheToken, ConcurrentDictionary<(Type, ulong), object>> _structureMap;

    /// <summary>
    /// Returns the structure of type <typeparamref name="T" /> at <paramref name="address" />,
    /// decoding it through <paramref name="context" /> on a miss.
    /// </summary>
    /// <remarks>
    /// Not <c>async</c>: a hit is the common case and must not pay for a state machine. The decode
    /// path seeks first, so a caller does NOT need to position the driver - and must not rely on
    /// where the driver ends up, because on a hit it is not moved at all.
    /// <para>
    /// Two operations missing on the same structure both decode it and one result is discarded -
    /// wasted work, never incorrect, and the installed instance is preferred so that everyone shares
    /// one set of node caches.
    /// </para>
    /// </remarks>
    public static ValueTask<T> GetStructure<T>(
        NativeReadContext context,
        ulong address,
        Func<NativeReadContext, ValueTask<T>> decode)
        where T : class
    {
        var addressToStructureMap = _structureMap.GetOrAdd(
            context.CacheToken,
            static _ => new ConcurrentDictionary<(Type, ulong), object>());

        // The type is part of the key because a generic structure has one entry per instantiation
        // (BTree2Header<BTree2Record05> and BTree2Header<BTree2Record08> are different types), and
        // because nothing otherwise stops two different structure kinds from sharing an address.
        var key = (typeof(T), address);

        if (addressToStructureMap.TryGetValue(key, out var cached))
            return new ValueTask<T>((T)cached);

        return DecodeStructure(context, address, decode, addressToStructureMap, key);
    }

    /// <summary>
    /// As <see cref="GetStructure{T}" />, but threads <paramref name="state" /> through to
    /// <paramref name="decode" /> so that the decoder can stay a static, allocation-free lambda even
    /// when it needs an extra argument - a key decoder, for instance.
    /// </summary>
    public static ValueTask<T> GetStructure<T, TState>(
        NativeReadContext context,
        ulong address,
        TState state,
        Func<NativeReadContext, TState, ValueTask<T>> decode)
        where T : class
    {
        var addressToStructureMap = _structureMap.GetOrAdd(
            context.CacheToken,
            static _ => new ConcurrentDictionary<(Type, ulong), object>());

        var key = (typeof(T), address);

        if (addressToStructureMap.TryGetValue(key, out var cached))
            return new ValueTask<T>((T)cached);

        return DecodeStructure(context, address, state, decode, addressToStructureMap, key);
    }

    private static ValueTask<T> DecodeStructure<T>(
        NativeReadContext context,
        ulong address,
        Func<NativeReadContext, ValueTask<T>> decode,
        ConcurrentDictionary<(Type, ulong), object> addressToStructureMap,
        (Type, ulong) key)
        where T : class
    {
        return DecodeStructure(
            context,
            address,
            decode,
            static (c, d) => d(c),
            addressToStructureMap,
            key);
    }

    private static async ValueTask<T> DecodeStructure<T, TState>(
        NativeReadContext context,
        ulong address,
        TState state,
        Func<NativeReadContext, TState, ValueTask<T>> decode,
        ConcurrentDictionary<(Type, ulong), object> addressToStructureMap,
        (Type, ulong) key)
        where T : class
    {
        context.Driver.SeekRelativeToBaseAddress((long)address);

        var structure = await decode(context, state).ConfigureAwait(false);

        return (T)addressToStructureMap.GetOrAdd(key, structure);
    }

    #endregion

    #region Global Heap

    // BOUNDED, and by BYTES rather than by entry count: a collection holds decoded variable-length
    // payload, so this is the one cache in the reader whose footprint would otherwise grow with how
    // much data has been read and be released only when the file closes. A collection is at least
    // 4 KiB with no upper bound - a single large value gets one to itself - so counting entries would
    // bound nothing. See BoundedAddressCache.
    private static readonly ConcurrentDictionary<NativeCacheToken, BoundedAddressCache<GlobalHeapCollection>> _globalHeapMap;

    /// <summary>
    /// Returns the global heap collection at <paramref name="address" />, decoding it on a miss.
    /// </summary>
    /// <remarks>
    /// Not <c>async</c>, so that a hit - the common case, since a variable-length decode resolves many
    /// elements out of the same collection - costs a dictionary lookup and no state machine. A miss
    /// awaits <c>GlobalHeapCollection.Decode</c> rather than blocking on it, which is what lets a
    /// variable-length read honour the public async surface.
    /// </remarks>
    public static ValueTask<GlobalHeapCollection> GetGlobalHeapObject(
        NativeReadContext context,
        ulong address,
        bool restoreAddress = false)
    {
        // GetOrAdd (not AddOrUpdate-with-a-constant-new-value) so that
        // when two threads both miss on the same token, the first-installed map wins and
        // is shared by both, instead of one thread's map (and anything decoded into it)
        // being silently discarded.
        // The budget comes from the caller's read options, so a process holding many files open can
        // cap what all of them together retain - the cache is per file.
        var addressToCollectionMap = _globalHeapMap.GetOrAdd(
            context.CacheToken,
            static (_, budget) => new BoundedAddressCache<GlobalHeapCollection>(budget),
            context.ReadOptions.GlobalHeapCacheByteBudget);

        if (addressToCollectionMap.TryGetValue(address, out var collection))
            return new ValueTask<GlobalHeapCollection>(collection);

        return DecodeGlobalHeapObject(context, address, restoreAddress, addressToCollectionMap);
    }

    private static async ValueTask<GlobalHeapCollection> DecodeGlobalHeapObject(
        NativeReadContext context,
        ulong address,
        bool restoreAddress,
        BoundedAddressCache<GlobalHeapCollection> addressToCollectionMap)
    {
        // This seek-decode-restore is why concurrent reads of
        // variable-length data need a driver per read operation and not merely a thread-safe
        // cache - it moves the cursor in the middle of a dataset/attribute decode. `context`
        // is the caller's context, so on a read path that is the operation driver.
        var position = context.Driver.Position;

        context.Driver.SeekRelativeToBaseAddress((long)address);

        var collection = await GlobalHeapCollection.Decode(context).ConfigureAwait(false);

        // Prefer the installed instance, like the other caches here, so two threads missing on the
        // same collection converge on one rather than one silently discarding its decode.
        //
        // CollectionSize is the on-disk size of the collection, which is what its decoded objects add
        // up to bar per-object overhead - the right cost to charge the byte budget.
        collection = addressToCollectionMap.GetOrAdd(address, collection, (long)collection.CollectionSize);

        if (restoreAddress)
            context.Driver.Seek(position, SeekOrigin.Begin);

        return collection;
    }

    #endregion

    #region File Handles

    // Keyed by NativeReadContext.CacheToken, like the global heap cache above, so entries are per
    // FILE rather than per driver instance - a driver is a per-read-operation allocation now, and
    // keying on it would leak an entry per read.
    private static readonly ConcurrentDictionary<NativeCacheToken, ConcurrentDictionary<string, NativeFile>> _fileMap;

    public static NativeFile GetNativeFile(NativeReadContext context, string absoluteFilePath)
    {
        if (!Uri.TryCreate(absoluteFilePath, UriKind.Absolute, out var uri))
            throw new Exception("The provided path is not absolute.");

        if (!uri.IsFile && !uri.IsUnc)
            throw new Exception("The provided path is not a file path or a UNC path.");

        var pathToNativeFileMap = _fileMap.GetOrAdd(
            context.CacheToken,
            static _ => new ConcurrentDictionary<string, NativeFile>());

        if (!pathToNativeFileMap.TryGetValue(uri.AbsoluteUri, out var nativeFile))
        {
            // TODO: This does not correspond to https://support.hdfgroup.org/HDF5/doc/RM/H5L/H5Lcreate_external.htm
            var opened = H5File.Open(uri.LocalPath, FileMode.Open, FileAccess.Read, FileShare.Read);

            nativeFile = pathToNativeFileMap.GetOrAdd(uri.AbsoluteUri, opened);

            // Losing the race means this handle is not the one anybody will use, and Clear only
            // disposes what is in the map - so without this it would stay open until the process ends.
            if (!ReferenceEquals(nativeFile, opened))
                opened.Dispose();
        }

        return nativeFile;
    }

    #endregion
}