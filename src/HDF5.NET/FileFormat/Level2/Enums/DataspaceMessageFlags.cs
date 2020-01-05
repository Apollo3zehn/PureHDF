using System;

namespace HDF5.NET
{
    [Flags]
    public enum DataspaceMessageFlags : byte
    {
        MaximumDimensions = 1,
        PermuationIndices = 2
    }
}
