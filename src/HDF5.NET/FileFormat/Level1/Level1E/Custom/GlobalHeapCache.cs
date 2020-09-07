using System.Collections.Generic;
using System.IO;

namespace HDF5.NET
{
    internal static class GlobalHeapCache
    {
        #region Fields

        private static Dictionary<Superblock, Dictionary<ulong, GlobalHeapCollection>> _map;

        #endregion

        #region Constructors

        static GlobalHeapCache()
        {
            _map = new Dictionary<Superblock, Dictionary<ulong, GlobalHeapCollection>>();
        }

        #endregion

        #region Methods

        public static GlobalHeapCollection GetGlobalHeapObject(H5BinaryReader reader, Superblock superblock, ulong address)
        {
            if (!_map.TryGetValue(superblock, out var addressToCollectionMap))
            {
                addressToCollectionMap = new Dictionary<ulong, GlobalHeapCollection>();
                _map[superblock] = addressToCollectionMap;
            }

            if (!addressToCollectionMap.TryGetValue(address, out var collection))
            {
                collection = GlobalHeapCache.ReadGlobalHeapCollection(reader, superblock, address);
                addressToCollectionMap[address] = collection;
            }

            return collection;
        }

        public static void Clear(Superblock superblock)
        {
            if (_map.ContainsKey(superblock))
                _map.Remove(superblock);
        }

        private static GlobalHeapCollection ReadGlobalHeapCollection(H5BinaryReader reader, Superblock superblock, ulong address)
        {
            reader.Seek((long)address, SeekOrigin.Begin);
            return new GlobalHeapCollection(reader, superblock);
        }

        #endregion
    }
}
