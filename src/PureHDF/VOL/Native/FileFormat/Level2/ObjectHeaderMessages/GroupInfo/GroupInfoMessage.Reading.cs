namespace PureHDF.VOL.Native;

internal partial record class GroupInfoMessage(
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

    public static async ValueTask<GroupInfoMessage> Decode(H5DriverBase driver)
    {
        // version
        var version = await driver.ReadByte().ConfigureAwait(false);

        // flags
        var flags = (GroupInfoMessageFlags)await driver.ReadByte().ConfigureAwait(false);

        // maximum compact value and minimum dense value
        var maximumCompactValue = default(ushort);
        var minimumDenseValue = default(ushort);

        if (flags.HasFlag(GroupInfoMessageFlags.StoreLinkPhaseChangeValues))
        {
            maximumCompactValue = await driver.ReadUInt16().ConfigureAwait(false);
            minimumDenseValue = await driver.ReadUInt16().ConfigureAwait(false);
        }

        // estimated entry count and estimated entry link name length
        var estimatedEntryCount = default(ushort);
        var estimatedEntryLinkNameLength = default(ushort);

        if (flags.HasFlag(GroupInfoMessageFlags.StoreNonDefaultEntryInformation))
        {
            estimatedEntryCount = await driver.ReadUInt16().ConfigureAwait(false);
            estimatedEntryLinkNameLength = await driver.ReadUInt16().ConfigureAwait(false);
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