namespace PureHDF.VOL.Native;

[Flags]
internal enum GroupInfoMessageFlags : byte
{
    StoreLinkPhaseChangeValues = 1,
    StoreNonDefaultEntryInformation = 2
}