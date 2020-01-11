namespace HDF5.NET
{
    public enum LayoutClass : byte
    {
        Compact = 0,
        Contiguous = 1,
        Chunked = 2,
        VirtualStorage = 3
    }
}
