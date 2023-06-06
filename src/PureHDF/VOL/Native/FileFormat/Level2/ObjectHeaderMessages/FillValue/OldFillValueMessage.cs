namespace PureHDF.VOL.Native;

internal record class OldFillValueMessage(
    byte[] FillValue
) : Message
{
    public static OldFillValueMessage Decode(H5DriverBase driver)
    {
        var size = driver.ReadUInt32();
        var fillValue = driver.ReadBytes((int)size);

        return new OldFillValueMessage(
            FillValue: fillValue
        );
    }
}