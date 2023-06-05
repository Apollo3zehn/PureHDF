namespace PureHDF.VOL.Native;

internal class ObjectModificationMessage : Message
{
    #region Fields

    private byte _version;

    #endregion

    #region Constructors

    public ObjectModificationMessage(H5DriverBase driver)
    {
        // version
        Version = driver.ReadByte();

        // reserved
        driver.ReadBytes(3);

        // seconds after unix epoch
        SecondsAfterUnixEpoch = driver.ReadUInt32();
    }

    public ObjectModificationMessage(uint secondsAfterUnixEpoch)
    {
        // version
        Version = 1;

        // seconds after unix epoch
        SecondsAfterUnixEpoch = secondsAfterUnixEpoch;
    }

    #endregion

    #region Properties

    public byte Version
    {
        get
        {
            return _version;
        }
        set
        {
            if (value != 1)
                throw new FormatException($"Only version 1 instances of type {nameof(ObjectModificationMessage)} are supported.");

            _version = value;
        }
    }

    public uint SecondsAfterUnixEpoch { get; set; }

    #endregion
}