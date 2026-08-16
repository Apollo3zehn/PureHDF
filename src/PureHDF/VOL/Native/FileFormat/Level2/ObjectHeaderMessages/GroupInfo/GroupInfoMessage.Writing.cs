namespace PureHDF.VOL.Native;

internal partial record class GroupInfoMessage
{
    public override ushort GetEncodeSize()
    {
        var size =
            sizeof(byte) +
            sizeof(byte) +
            (
                Flags.HasFlag(GroupInfoMessageFlags.StoreLinkPhaseChangeValues)
                    ? sizeof(ushort) + sizeof(ushort)
                    : 0
            ) +
            (
                Flags.HasFlag(GroupInfoMessageFlags.StoreNonDefaultEntryInformation)
                    ? sizeof(ushort) + sizeof(ushort)
                    : 0
            );

        return (ushort)size;
    }

    public override void Encode(H5DriverBase driver)
    {
        // version
        driver.Write(Version);

        // flags
        driver.Write((byte)Flags);

        // maximum compact value and minimum dense value
        if (Flags.HasFlag(GroupInfoMessageFlags.StoreLinkPhaseChangeValues))
        {
            driver.Write(MaximumCompactValue);
            driver.Write(MinimumDenseValue);
        }

        // estimated entry count and estimated entry link name length
        if (Flags.HasFlag(GroupInfoMessageFlags.StoreNonDefaultEntryInformation))
        {
            driver.Write(EstimatedEntryCount);
            driver.Write(EstimatedEntryLinkNameLength);
        }
    }
}
