namespace PureHDF.VOL.Native;

internal readonly partial record struct FilterDescription(
    ushort Identifier,
    FilterFlags Flags,
    string Name,
    uint[] ClientData
)
{
    public static FilterDescription Decode(H5DriverBase driver, byte version)
    {
        // filter identifier
        var identifier = driver.ReadUInt16();

        // name length
        var nameLength = version switch
        {
            1 => driver.ReadUInt16(),
            2 when identifier >= 256 => driver.ReadUInt16(),
            2 when identifier < 256 => 0,
            _ => throw new NotSupportedException($"Only version 1 or 2 instances of the {nameof(FilterDescription)} type are supported.")
        };

        // flags
        var flags = (FilterFlags)driver.ReadUInt16();

        // client data value count
        var clientDataValueCount = driver.ReadUInt16();

        // name
        var name = (nameLength, version) switch
        {
            (0, _) => string.Empty,
            (_, 1) => ReadUtils.ReadNullTerminatedString(driver, pad: true),
            (_, 2) => ReadUtils.ReadFixedLengthString(driver, nameLength),
            _ => throw new Exception($"Filter pipeline version {version} is not supported.")
        };

        // client data
        var clientData = new uint[clientDataValueCount];

        for (ushort i = 0; i < clientDataValueCount; i++)
        {
            clientData[i] = driver.ReadUInt32();
        }

        // padding
        if (version == 1 && clientDataValueCount % 2 != 0)
            driver.ReadBytes(4);

        return new FilterDescription(
            Identifier: identifier,
            Flags: flags,
            Name: name,
            ClientData: clientData
        );
    }
}