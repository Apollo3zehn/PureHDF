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

    public static BTreeKValuesMessage Decode(H5DriverBase driver)
    {
        var version = driver.ReadByte();

        return new BTreeKValuesMessage(
            IndexedStorageInternalNodeK: driver.ReadUInt16(),
            GroupInternalNodeK: driver.ReadUInt16(),
            GroupLeafNodeK: driver.ReadUInt16()
        )
        {
            Version = version
        };
    }
}