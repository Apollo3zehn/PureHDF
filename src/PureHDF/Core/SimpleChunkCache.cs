namespace PureHDF
{
    // https://support.hdfgroup.org/HDF5/doc/H5.user/Caching.html
    // https://support.hdfgroup.org/HDF5/faq/perfissues.html
    partial class SimpleChunkCache : IChunkCache
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

        #region Methods

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

        #endregion
    }
}
