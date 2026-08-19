namespace PureHDF.VOL.Native;

internal record class ObjectCommentMessage(
    string Comment
) : Message
{
    public static async ValueTask<ObjectCommentMessage> Decode(H5DriverBase driver)
    {
        return new ObjectCommentMessage(
            Comment: await ReadUtils.ReadNullTerminatedString(driver, pad: false).ConfigureAwait(false)
        );
    }
}