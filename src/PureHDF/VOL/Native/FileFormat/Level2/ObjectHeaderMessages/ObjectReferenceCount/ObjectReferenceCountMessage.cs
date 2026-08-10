namespace PureHDF.VOL.Native;

internal partial record class ObjectReferenceCountMessage(
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

    public static async ValueTask<ObjectReferenceCountMessage> Decode(H5DriverBase driver)
    {
        var version = await driver.ReadByte().ConfigureAwait(false);

        return new ObjectReferenceCountMessage(
            ReferenceCount: await driver.ReadUInt32().ConfigureAwait(false)
        )
        {
            Version = version
        };
    }
}