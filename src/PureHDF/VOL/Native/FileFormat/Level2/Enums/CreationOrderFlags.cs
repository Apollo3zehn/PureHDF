namespace PureHDF.VOL.Native;

[Flags]
internal enum CreationOrderFlags : byte
{
    TrackCreationOrder = 1,
    IndexCreationOrder = 2
}