namespace PureHDF.VOL.Native;

internal record class ObjectHeaderContinuationMessage(
    ulong Offset,
    ulong Length
) : Message
{
    public static ObjectHeaderContinuationMessage Decode(NativeContext context)
    {
        var (driver, superblock) = context;

        return new ObjectHeaderContinuationMessage(
            Offset: superblock.ReadOffset(driver),
            Length: superblock.ReadLength(driver)
        );
    }
}