namespace PureHDF;

internal class FilterDescription
{
    #region Constructors

    public FilterDescription(H5DriverBase driver, byte version)
    {
        // filter identifier
        Identifier = (FilterIdentifier)driver.ReadInt16();

        // name length
        var nameLength = version switch
        {
            1 => driver.ReadUInt16(),
            2 when (ushort)Identifier >= 256 => driver.ReadUInt16(),
            2 when (ushort)Identifier < 256 => 0,
            _ => throw new NotSupportedException($"Only version 1 or 2 instances of the {nameof(FilterDescription)} type are supported.")
        };

        // flags
        Flags = (FilterFlags)driver.ReadUInt16();

        // client data value count
        var clientDataValueCount = driver.ReadUInt16();

        // name
        Name = nameLength > 0 ? ReadUtils.ReadNullTerminatedString(driver, pad: true) : string.Empty;

        // client data
        ClientData = new uint[clientDataValueCount];

        for (ushort i = 0; i < clientDataValueCount; i++)
        {
            ClientData[i] = driver.ReadUInt32();
        }

        // padding
        if (version == 1 && clientDataValueCount % 2 != 0)
            driver.ReadBytes(4);
    }

    #endregion

    #region Properties

    public FilterIdentifier Identifier { get; set; }
    public FilterFlags Flags { get; set; }
    public string Name { get; set; }
    public uint[] ClientData { get; set; }

    #endregion
}