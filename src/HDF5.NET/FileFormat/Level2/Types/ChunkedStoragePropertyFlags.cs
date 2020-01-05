using System;

namespace HDF5.NET
{
    [Flags]
    public enum ChunkedStoragePropertyFlags : byte
    {
        DONT_FILTER_PARTIAL_BOUND_CHUNKS = 1,
        SINGLE_INDEX_WITH_FILTER = 2
    }
}
