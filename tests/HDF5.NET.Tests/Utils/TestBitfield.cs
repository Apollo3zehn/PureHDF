namespace HDF5.NET.Tests
{
    [Flags]
    public enum TestBitfield : ushort
    {
        a = 0b0000_0001,
        b = 0b0000_0010,
        c = 0b0000_0100,
        d = 0b0000_1000,
        e = 0b0001_0000,
        f = 0b0010_0000,
        g = 0b0100_0000,
        h = 0b1000_0000
    }
}
