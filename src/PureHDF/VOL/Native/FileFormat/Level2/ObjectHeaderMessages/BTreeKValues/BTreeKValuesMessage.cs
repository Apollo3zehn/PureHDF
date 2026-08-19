namespace PureHDF.VOL.Native;

internal record class BTreeKValuesMessage(
    ushort IndexedStorageInternalNodeK,
    ushort GroupInternalNodeK,
    ushort GroupLeafNodeK
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
                throw new FormatException($"Only version 0 instances of type {nameof(BTreeKValuesMessage)} are supported.");

            _version = value;
        }
    }

    public static async ValueTask<BTreeKValuesMessage> Decode(H5DriverBase driver)
    {
        var version = await driver.ReadByte().ConfigureAwait(false);

        return new BTreeKValuesMessage(
            IndexedStorageInternalNodeK: await driver.ReadUInt16().ConfigureAwait(false),
            GroupInternalNodeK: await driver.ReadUInt16().ConfigureAwait(false),
            GroupLeafNodeK: await driver.ReadUInt16().ConfigureAwait(false)
        )
        {
            Version = version
        };
    }
}