namespace PureHDF;

internal static partial class ReadUtils
{
    public static ulong ReadUlong(H5DriverBase driver, ulong size)
    {
        return size switch
        {
            1 => driver.ReadByte(),
            2 => driver.ReadUInt16(),
            4 => driver.ReadUInt32(),
            8 => driver.ReadUInt64(),
            _ => ReadUlongArbitrary(driver, size)
        };
    }

    private static ulong ReadUlongArbitrary(H5DriverBase driver, ulong size)
    {
        var result = 0UL;
        var shift = 0;

        for (ulong i = 0; i < size; i++)
        {
            var value = driver.ReadByte();
            result += (ulong)(value << shift);
            shift += 8;
        }

        return result;
    }
}