namespace PureHDF.VOL.Native;

internal partial record class ObjectReferenceCountMessage
{
    public override ushort GetEncodeSize()
    {
        return sizeof(byte) + sizeof(uint);
    }

    public override void Encode(H5DriverBase driver)
    {
        // version
        driver.Write(_version);

        // reference count
        driver.Write(ReferenceCount);
    }
}
