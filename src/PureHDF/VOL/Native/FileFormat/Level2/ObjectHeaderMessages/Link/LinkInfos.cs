namespace PureHDF.VOL.Native;

internal abstract record class LinkInfo();

internal record class HardLinkInfo(
    ulong HeaderAddress
) : LinkInfo
{
    public static HardLinkInfo Decode(NativeContext context)
    {
        var (driver, superblock) = context;

        return new HardLinkInfo(
            HeaderAddress: superblock.ReadOffset(driver)
        );
    }
}

internal record class SoftLinkInfo(
    string Value
) : LinkInfo
{
    public static SoftLinkInfo Decode(H5DriverBase driver)
    {
        var valueLength = driver.ReadUInt16();
        var value = ReadUtils.ReadFixedLengthString(driver, valueLength);

        return new SoftLinkInfo(
            Value: value
        );
    }
}