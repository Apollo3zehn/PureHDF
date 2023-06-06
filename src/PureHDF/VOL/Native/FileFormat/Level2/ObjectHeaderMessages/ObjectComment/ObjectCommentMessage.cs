namespace PureHDF.VOL.Native;

internal record class ObjectCommentMessage(
    string Comment
) : Message
{
    public static ObjectCommentMessage Decode(H5DriverBase driver)
    {
        return new ObjectCommentMessage(
            Comment: ReadUtils.ReadNullTerminatedString(driver, pad: false)
        );
    }
}