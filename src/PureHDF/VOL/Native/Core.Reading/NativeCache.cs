using System.Collections.Concurrent;

namespace PureHDF.VOL.Native;

internal static class NativeCache
{
    #region Constructors

    static NativeCache()
    {
        _globalHeapMap = new ConcurrentDictionary<NativeCacheToken, ConcurrentDictionary<ulong, GlobalHeapCollection>>();
        _fileMap = new ConcurrentDictionary<NativeCacheToken, ConcurrentDictionary<string, NativeFile>>();
    }

    #endregion

    #region Shared

    public static void Clear(NativeReadContext context)
    {
        // global heap
        _globalHeapMap.TryRemove(context.CacheToken, out var _);

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

    #region Global Heap

    private static readonly ConcurrentDictionary<NativeCacheToken, ConcurrentDictionary<ulong, GlobalHeapCollection>> _globalHeapMap;

    public static GlobalHeapCollection GetGlobalHeapObject(
        NativeReadContext context,
        ulong address,
        bool restoreAddress = false)
    {
        // NOTE (race fix): GetOrAdd (not AddOrUpdate-with-a-constant-new-value) so that
        // when two threads both miss on the same token, the first-installed map wins and
        // is shared by both, instead of one thread's map (and anything decoded into it)
        // being silently discarded.
        var addressToCollectionMap = _globalHeapMap.GetOrAdd(
            context.CacheToken,
            static _ => new ConcurrentDictionary<ulong, GlobalHeapCollection>());

        if (!addressToCollectionMap.TryGetValue(address, out var collection))
        {
            // NOTE (per-operation drivers): this seek-decode-restore is why concurrent reads of
            // variable-length data need a driver per read operation and not merely a thread-safe
            // cache - it moves the cursor in the middle of a dataset/attribute decode. `context`
            // is the caller's context, so on a read path that is the operation driver.
            var position = context.Driver.Position;

            context.Driver.SeekRelativeToBaseAddress((long)address);

            // NOTE (async propagation): GlobalHeapCollection.Decode is now async.
            // This method is called from a constructor (H5D_Virtual) and other
            // fully synchronous call sites with no async counterpart, and cannot
            // itself become async, so the call is bridged here — see report.
            collection = GlobalHeapCollection.Decode(context).GetAwaiter().GetResult();

            addressToCollectionMap[address] = collection;

            if (restoreAddress)
                context.Driver.Seek(position, SeekOrigin.Begin);
        }

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