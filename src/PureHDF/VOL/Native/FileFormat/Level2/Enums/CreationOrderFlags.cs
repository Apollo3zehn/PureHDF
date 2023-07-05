namespace PureHDF.VOL.Native;

[Flags]
internal enum CreationOrderFlags : byte
{
    None = 0,
    TrackCreationOrder = 1,
    IndexCreationOrder = 2
}