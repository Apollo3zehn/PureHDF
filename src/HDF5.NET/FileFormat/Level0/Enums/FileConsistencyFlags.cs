using System;

namespace HDF5.NET
{
    [Flags]
    public enum FileConsistencyFlags
    {
        WriteAccess = 1,
        SWMR = 4
    }
}
