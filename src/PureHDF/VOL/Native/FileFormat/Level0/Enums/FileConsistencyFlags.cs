namespace PureHDF.VOL.Native;

[Flags]
internal enum FileConsistencyFlags : byte
{
    WriteAccess = 1,
    SWMR = 4
}