namespace PureHDF.VOL.Native;

internal record class OldFillValueMessage(
    byte[] FillValue
) : Message
{
    public static async ValueTask<OldFillValueMessage> Decode(H5DriverBase driver)
    {
        var size = await driver.ReadUInt32().ConfigureAwait(false);
        var fillValue = await driver.ReadBytes((int)size).ConfigureAwait(false);

        return new OldFillValueMessage(
            FillValue: fillValue
        );
    }
}