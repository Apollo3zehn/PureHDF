namespace PureHDF.VOL.Native;

internal record class ObjectModificationMessage(
    uint SecondsAfterUnixEpoch
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
            if (value != 1)
                throw new FormatException($"Only version 1 instances of type {nameof(ObjectModificationMessage)} are supported.");

            _version = value;
        }
    }

    public static ObjectModificationMessage Decode(H5DriverBase driver)
    {
        // version
        var version = driver.ReadByte();

        // reserved
        driver.ReadBytes(3);

        // seconds after unix epoch
        var secondsAfterUnixEpoch = driver.ReadUInt32();

        return new ObjectModificationMessage(
            SecondsAfterUnixEpoch: secondsAfterUnixEpoch
        )
        {
            Version = version
        };
    }
}