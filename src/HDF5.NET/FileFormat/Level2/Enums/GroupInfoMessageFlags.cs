namespace HDF5.NET
{
    [Flags]
    internal enum GroupInfoMessageFlags : byte
    {
        StoreLinkPhaseChangeValues = 1,
        StoreNonDefaultEntryInformation = 2
    }
}
