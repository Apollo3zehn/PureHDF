namespace PureHDF.VOL.Native;

internal class ObjectHeaderContinuationMessage : Message
{
    #region Constructors

    public ObjectHeaderContinuationMessage(NativeContext context)
    {
        var (driver, superblock) = context;

        Offset = superblock.ReadOffset(driver);
        Length = superblock.ReadLength(driver);
    }

    #endregion

    #region Properties

    public ulong Offset { get; set; }
    public ulong Length { get; set; }

    #endregion
}