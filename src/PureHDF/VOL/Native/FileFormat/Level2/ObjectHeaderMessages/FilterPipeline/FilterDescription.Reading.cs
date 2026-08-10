namespace PureHDF.VOL.Native;

internal readonly partial record struct FilterDescription(
    ushort Identifier,
    FilterFlags Flags,
    string Name,
    uint[] ClientData
)
{
    public static async ValueTask<FilterDescription> Decode(H5DriverBase driver, byte version)
    {
        // filter identifier
        var identifier = await driver.ReadUInt16().ConfigureAwait(false);

        // name length
        var nameLength = version switch
        {
            1 => await driver.ReadUInt16().ConfigureAwait(false),
            2 when identifier >= 256 => await driver.ReadUInt16().ConfigureAwait(false),
            2 when identifier < 256 => 0,
            _ => throw new NotSupportedException($"Only version 1 or 2 instances of the {nameof(FilterDescription)} type are supported.")
        };

        // flags
        var flags = (FilterFlags)await driver.ReadUInt16().ConfigureAwait(false);

        // client data value count
        var clientDataValueCount = await driver.ReadUInt16().ConfigureAwait(false);

        // name
        var name = (nameLength, version) switch
        {
            (0, _) => string.Empty,
            (_, 1) => await ReadUtils.ReadNullTerminatedString(driver, pad: true).ConfigureAwait(false),
            (_, 2) => await ReadUtils.ReadFixedLengthString(driver, nameLength).ConfigureAwait(false),
            _ => throw new Exception($"Filter pipeline version {version} is not supported.")
        };

        // client data
        var clientData = new uint[clientDataValueCount];

        for (ushort i = 0; i < clientDataValueCount; i++)
        {
            clientData[i] = await driver.ReadUInt32().ConfigureAwait(false);
        }

        // padding
        if (version == 1 && clientDataValueCount % 2 != 0)
            await driver.ReadBytes(4).ConfigureAwait(false);

        return new FilterDescription(
            Identifier: identifier,
            Flags: flags,
            Name: name,
            ClientData: clientData
        );
    }
}