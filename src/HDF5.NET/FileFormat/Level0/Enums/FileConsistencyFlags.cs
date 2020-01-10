using System;

namespace HDF5.NET
{
    [Flags]
    public enum FileConsistencyFlags : byte
    {
        WriteAccess = 1,
        SWMR = 4
    }
}
