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

    public static async ValueTask<ObjectModificationMessage> Decode(H5DriverBase driver)
    {
        // version
        var version = await driver.ReadByte().ConfigureAwait(false);

        // reserved
        await driver.ReadBytes(3).ConfigureAwait(false);

        // seconds after unix epoch
        var secondsAfterUnixEpoch = await driver.ReadUInt32().ConfigureAwait(false);

        return new ObjectModificationMessage(
            SecondsAfterUnixEpoch: secondsAfterUnixEpoch
        )
        {
            Version = version
        };
    }
}