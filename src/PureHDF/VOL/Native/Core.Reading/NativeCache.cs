using System.Collections.Concurrent;

namespace PureHDF.VOL.Native;

internal static class NativeCache
{
    #region Constructors

    static NativeCache()
    {
        _globalHeapMap = new ConcurrentDictionary<H5DriverBase, ConcurrentDictionary<ulong, GlobalHeapCollection>>();
        _fileMap = new ConcurrentDictionary<H5DriverBase, ConcurrentDictionary<string, NativeFile>>();
    }

    #endregion

    #region Shared

    public static void Clear(H5DriverBase driver)
    {
        // global heap
        _globalHeapMap.TryRemove(driver, out var _);

        // file map
        if (_fileMap.TryRemove(driver, out var pathToNativeFileMap))
        {
            foreach (var nativeFile in pathToNativeFileMap.Values)
            {
                nativeFile.Dispose();
            }
        }
    }

    #endregion

    #region Global Heap

    private static readonly ConcurrentDictionary<H5DriverBase, ConcurrentDictionary<ulong, GlobalHeapCollection>> _globalHeapMap;

    public static GlobalHeapCollection GetGlobalHeapObject(
        NativeReadContext context,
        ulong address,
        bool restoreAddress = false)
    {
        // GetOrAdd rather than AddOrUpdate with a constant new value: the update lambda returned the
        // freshly created map unconditionally, so when two readers both missed, one reader's map -
        // and everything already decoded into it - was silently dropped.
        var addressToCollectionMap = _globalHeapMap.GetOrAdd(
            context.Driver,
            static _ => new ConcurrentDictionary<ulong, GlobalHeapCollection>());

        if (!addressToCollectionMap.TryGetValue(address, out var collection))
        {
            var position = context.Driver.Position;

            context.Driver.SeekRelativeToBaseAddress((long)address);
            collection = GlobalHeapCollection.Decode(context);

            // Prefer whatever is already installed, so two readers that miss on the same collection
            // converge on one instance instead of one of them discarding its work.
            collection = addressToCollectionMap.GetOrAdd(address, collection);

            if (restoreAddress)
                context.Driver.Seek(position, SeekOrigin.Begin);
        }

        return collection;
    }

    #endregion

    #region File Handles

    private static readonly ConcurrentDictionary<H5DriverBase, ConcurrentDictionary<string, NativeFile>> _fileMap;

    public static NativeFile GetNativeFile(H5DriverBase driver, string absoluteFilePath)
    {
        if (!Uri.TryCreate(absoluteFilePath, UriKind.Absolute, out var uri))
            throw new Exception("The provided path is not absolute.");

        if (!uri.IsFile && !uri.IsUnc)
            throw new Exception("The provided path is not a file path or a UNC path.");

        var pathToNativeFileMap = _fileMap.GetOrAdd(
            driver,
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