namespace PureHDF.VOL.Native;

internal partial record class ObjectReferenceCountMessage
{
    /// <summary>
    /// Where the count was last encoded, so that a shared object's header can be given its final count
    /// without encoding it again.
    /// </summary>
    public long CountPosition { get; private set; }

    public override ushort GetEncodeSize()
    {
        return sizeof(byte) + sizeof(uint);
    }

    public override void Encode(H5DriverBase driver)
    {
        // version
        driver.Write(_version);

        // reference count
        CountPosition = driver.Position;
        driver.Write(ReferenceCount);
    }
}
