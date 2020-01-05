namespace HDF5.NET
{
    public enum SharedMessageLocation
    {
        NotSharedNotShareable = 0,
        SharedObjectHeaderMessageHeap = 1,
        AnotherObjectsHeader = 2,
        NotSharedShareable = 3,
    }
}
