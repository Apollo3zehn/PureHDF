namespace PureHDF.VOL.Native;

internal class ObjectCommentMessage : Message
{
    #region Constructors

    public ObjectCommentMessage(H5DriverBase driver)
    {
        // comment
        Comment = ReadUtils.ReadNullTerminatedString(driver, pad: false);
    }

    #endregion

    #region Properties

    public string Comment { get; set; }

    #endregion
}