namespace PureHDF.VOL.Native;

internal partial record class DataLayoutMessage4
{
    public override ushort GetEncodeSize()
    {
        var size =
            sizeof(byte) +
            sizeof(byte) +
            Properties.GetEncodeSize();

        return (ushort)size;
    }

    public override void Encode(BinaryWriter driver)
    {
        // version
        driver.Write(Version);

        // layout class
        driver.Write((byte)LayoutClass);

        // properties
        Properties.Encode(driver);
    }
}