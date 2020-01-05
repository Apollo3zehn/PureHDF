namespace HDF5.NET
{
    public enum FilterIdentifier : ushort
    {
        NA = 0,
        deflate = 1,
        shuffle = 2,
        fletcher32 = 3,
        szip = 4,
        nbit = 5,
        scaleoffset = 6
    }
}
