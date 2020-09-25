using System.Collections.Generic;
using System.IO;

namespace HDF5.NET
{
    internal static class H5Cache
    {
        #region Constructors

        static H5Cache()
        {
            _globalHeapMap = new Dictionary<Superblock, Dictionary<ulong, GlobalHeapCollection>>();
        }

        #endregion

        #region Shared

        public static void Clear(Superblock superblock)
        {
            if (_globalHeapMap.ContainsKey(superblock))
                _globalHeapMap.Remove(superblock);

            if (_fileHandleMap.ContainsKey(superblock))
                _fileHandleMap.Remove(superblock);
        }

        #endregion

        #region Global Heap

        private static Dictionary<Superblock, Dictionary<ulong, GlobalHeapCollection>> _globalHeapMap;

        public static GlobalHeapCollection GetGlobalHeapObject(H5BinaryReader reader, Superblock superblock, ulong address)
        {
            if (!_globalHeapMap.TryGetValue(superblock, out var addressToCollectionMap))
            {
                addressToCollectionMap = new Dictionary<ulong, GlobalHeapCollection>();
                _globalHeapMap[superblock] = addressToCollectionMap;
            }

            if (!addressToCollectionMap.TryGetValue(address, out var collection))
            {
                collection = H5Cache.ReadGlobalHeapCollection(reader, superblock, address);
                addressToCollectionMap[address] = collection;
            }

            return collection;
        }

        private static GlobalHeapCollection ReadGlobalHeapCollection(H5BinaryReader reader, Superblock superblock, ulong address)
        {
            reader.Seek((long)address, SeekOrigin.Begin);
            return new GlobalHeapCollection(reader, superblock);
        }

        #endregion

        #region File Handles

        private static Dictionary<Superblock, Dictionary<string, H5File>> _fileHandleMap;

        public static H5File GetH5File(Superblock superblock, string filePath)
        {
            if (!_fileHandleMap.TryGetValue(superblock, out var pathToH5FileMap))
            {
                pathToH5FileMap = new Dictionary<string, H5File>();
                _fileHandleMap[superblock] = pathToH5FileMap;
            }

            if (!pathToH5FileMap.TryGetValue(filePath, out var h5File))
            {
                h5File = H5File.Open(a, b, c, d);
                pathToH5FileMap[filePath] = h5File;
            }

            return h5File;
        }

        #endregion
    }
}
