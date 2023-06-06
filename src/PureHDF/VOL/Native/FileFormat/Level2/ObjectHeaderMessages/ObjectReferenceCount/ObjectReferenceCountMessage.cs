namespace PureHDF.VOL.Native;

internal record class ObjectReferenceCountMessage(
    uint ReferenceCount
) : Message
{
    private byte _version;

    public required byte Version
    {
        get
        {
            return _version;
        }
        init
        {
            if (value != 0)
                throw new FormatException($"Only version 0 instances of type {nameof(ObjectReferenceCountMessage)} are supported.");

            _version = value;
        }
    }

    public static ObjectReferenceCountMessage Decode(H5DriverBase driver)
    {
        var version = driver.ReadByte();

        return new ObjectReferenceCountMessage(
            ReferenceCount: driver.ReadUInt32()
        )
        {
            Version = version
        };
    }
}