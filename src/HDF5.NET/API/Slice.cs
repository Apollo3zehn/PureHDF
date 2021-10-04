namespace HDF5.NET
{
    public struct Slice
    {
        public ulong[] Coordinates { get; set; }

        public ulong Length { get; set; }
    }
}