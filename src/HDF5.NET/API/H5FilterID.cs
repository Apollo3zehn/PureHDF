namespace HDF5.NET
{
    public enum H5FilterID : ushort
    {
        NA = 0,
        Deflate = 1,
        Shuffle = 2,
        Fletcher32 = 3,
        Szip = 4,
        Nbit = 5,
        ScaleOffset = 6
    }
}
