namespace HDF5.NET
{
    [Flags]
    internal enum FileConsistencyFlags : byte
    {
        WriteAccess = 1,
        SWMR = 4
    }
}
