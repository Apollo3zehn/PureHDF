namespace HDF5.NET
{
    public static class H5Utils
    {
        public static bool IsPowerOfTwo(ulong x)
        {
            return (x != 0) && ((x & (x - 1)) == 0);
        }
    }
}
