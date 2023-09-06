using System.Collections.Concurrent;

namespace PureHDF.VOL.Native;

internal static class NativeCache
{
    #region Constructors

    static NativeCache()
    {
        _globalHeapMap = new ConcurrentDictionary<H5DriverBase, Dictionary<ulong, GlobalHeapCollection>>();
        _fileMap = new ConcurrentDictionary<H5DriverBase, Dictionary<string, NativeFile>>();
    }

    #endregion

    #region Shared

    public static void Clear(H5DriverBase driver)
    {
        // global heap
        if (_globalHeapMap.ContainsKey(driver))
            _globalHeapMap.TryRemove(driver, out var _);


        // file map
        if (_fileMap.TryGetValue(driver, out Dictionary<string, NativeFile>? value))
        {
            var pathToNativeFileMap = value;

            foreach (var nativeFile in pathToNativeFileMap.Values)
            {
                nativeFile.Dispose();
            }

            _fileMap.TryRemove(driver, out var _);
        }
    }

    #endregion

    #region Global Heap

    private static readonly ConcurrentDictionary<H5DriverBase, Dictionary<ulong, GlobalHeapCollection>> _globalHeapMap;

    public static GlobalHeapCollection GetGlobalHeapObject(
        NativeReadContext context, 
        ulong address,
        bool restoreAddress = false)
    {
        if (!_globalHeapMap.TryGetValue(context.Driver, out var addressToCollectionMap))
        {
            addressToCollectionMap = new Dictionary<ulong, GlobalHeapCollection>();
            _globalHeapMap.AddOrUpdate(context.Driver, addressToCollectionMap, (_, oldAddressToCollectionMap) => addressToCollectionMap);
        }

        if (!addressToCollectionMap.TryGetValue(address, out var collection))
        {
            var position = context.Driver.Position;

            context.Driver.Seek((long)address, SeekOrigin.Begin);
            collection = GlobalHeapCollection.Decode(context);

            addressToCollectionMap[address] = collection;

            if (restoreAddress)
                context.Driver.Seek(position, SeekOrigin.Begin);
        }

        return collection;
    }

    #endregion

    #region File Handles

    private static readonly ConcurrentDictionary<H5DriverBase, Dictionary<string, NativeFile>> _fileMap;

    public static NativeFile GetNativeFile(H5DriverBase driver, string absoluteFilePath, bool useAsync)
    {
        if (!Uri.TryCreate(absoluteFilePath, UriKind.Absolute, out var uri))
            throw new Exception("The provided path is not absolute.");

        if (!uri.IsFile && !uri.IsUnc)
            throw new Exception("The provided path is not a file path or a UNC path.");

        if (!_fileMap.TryGetValue(driver, out var pathToNativeFileMap))
        {
            pathToNativeFileMap = new Dictionary<string, NativeFile>();
            _fileMap.AddOrUpdate(driver, pathToNativeFileMap, (_, oldPathToNativeFileMap) => pathToNativeFileMap);
        }

        if (!pathToNativeFileMap.TryGetValue(uri.AbsoluteUri, out var nativeFile))
        {
            // TODO: This does not correspond to https://support.hdfgroup.org/HDF5/doc/RM/H5L/H5Lcreate_external.htm
            nativeFile = H5File.Open(uri.LocalPath, FileMode.Open, FileAccess.Read, FileShare.Read, useAsync: useAsync);
            pathToNativeFileMap[uri.AbsoluteUri] = nativeFile;
        }

        return nativeFile;
    }

    #endregion
}