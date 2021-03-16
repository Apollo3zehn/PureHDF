namespace HDF5.NET
{
    internal enum SpaceAllocationTime : byte
    {
        NotUsed = 0,
        EarlyAllocation = 1,
        LateAllocation = 2,
        IncrementalAllocation = 3
    }
}
