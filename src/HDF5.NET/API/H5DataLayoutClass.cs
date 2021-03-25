namespace HDF5.NET
{
    public enum H5DataLayoutClass : byte
    {
        Compact = 0,
        Contiguous = 1,
        Chunked = 2,
        VirtualStorage = 3
    }
}
