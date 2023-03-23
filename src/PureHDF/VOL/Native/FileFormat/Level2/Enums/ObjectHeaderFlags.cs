namespace PureHDF.VOL.Native;

[Flags]
internal enum ObjectHeaderFlags : byte
{
    SizeOfChunk1 = 1,
    SizeOfChunk2 = 2,
    TrackAttributeCreationOrder = 4,
    IndexAttributeCreationOrder = 8,
    StoreNonDefaultAttributePhaseChangeValues = 16,
    StoreFileAccessTimes = 32
}