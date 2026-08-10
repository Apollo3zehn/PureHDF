namespace PureHDF.VOL.Native;

internal record class ObjectHeaderContinuationMessage(
    ulong Offset,
    ulong Length
) : Message
{
    public static async ValueTask<ObjectHeaderContinuationMessage> Decode(NativeReadContext context)
    {
        var (driver, superblock) = context;

        var offset = await superblock.ReadOffset(driver).ConfigureAwait(false);
        var length = await superblock.ReadLength(driver).ConfigureAwait(false);

        return new ObjectHeaderContinuationMessage(
            Offset: offset,
            Length: length
        );
    }
}