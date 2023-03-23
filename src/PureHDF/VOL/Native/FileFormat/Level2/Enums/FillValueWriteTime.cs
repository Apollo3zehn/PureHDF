namespace PureHDF.VOL.Native;

internal enum FillValueWriteTime : byte
{
    OnAllocation = 0,
    Never = 1,
    IfSetByUser = 2
}