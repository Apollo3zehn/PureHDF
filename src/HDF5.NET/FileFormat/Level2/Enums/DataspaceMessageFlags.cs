using System;

namespace HDF5.NET
{
    [Flags]
    public enum DataspaceMessageFlags : byte
    {
        DimensionMaxSizes = 1,
        PermuationIndices = 2
    }
}
