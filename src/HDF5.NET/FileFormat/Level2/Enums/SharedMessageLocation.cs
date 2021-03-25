namespace HDF5.NET
{
    internal enum SharedMessageLocation : byte
    {
        NotSharedNotShareable = 0,
        SharedObjectHeaderMessageHeap = 1,
        AnotherObjectsHeader = 2,
        NotSharedShareable = 3,
    }
}
