namespace PureHDF.VOL.Native;

internal record class GroupInfoMessage(
    GroupInfoMessageFlags Flags,
    ushort MaximumCompactValue,
    ushort MinimumDenseValue,
    ushort EstimatedEntryCount,
    ushort EstimatedEntryLinkNameLength

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
                throw new FormatException($"Only version 0 instances of type {nameof(GroupInfoMessage)} are supported.");

            _version = value;
        }
    }

    public static GroupInfoMessage Decode(H5DriverBase driver)
    {
        // version
        var version = driver.ReadByte();

        // flags
        var flags = (GroupInfoMessageFlags)driver.ReadByte();

        // maximum compact value and minimum dense value
        var maximumCompactValue = default(ushort);
        var minimumDenseValue = default(ushort);

        if (flags.HasFlag(GroupInfoMessageFlags.StoreLinkPhaseChangeValues))
        {
            maximumCompactValue = driver.ReadUInt16();
            minimumDenseValue = driver.ReadUInt16();
        }

        // estimated entry count and estimated entry link name length
        var estimatedEntryCount = default(ushort);
        var estimatedEntryLinkNameLength = default(ushort);

        if (flags.HasFlag(GroupInfoMessageFlags.StoreNonDefaultEntryInformation))
        {
            estimatedEntryCount = driver.ReadUInt16();
            estimatedEntryLinkNameLength = driver.ReadUInt16();
        }

        return new GroupInfoMessage(
            Flags: flags,
            MaximumCompactValue: maximumCompactValue,
            MinimumDenseValue: minimumDenseValue,
            EstimatedEntryCount: estimatedEntryCount,
            EstimatedEntryLinkNameLength: estimatedEntryLinkNameLength
        )
        {
            Version = version
        };
    }
}