namespace HDF5.NET
{
    internal enum SpaceAllocationTime : byte
    {
        NotUsed = 0,
        Early = 1,
        Late = 2,
        Incremental = 3
    }
}
