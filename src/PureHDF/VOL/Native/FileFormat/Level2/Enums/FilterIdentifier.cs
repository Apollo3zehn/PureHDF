namespace PureHDF.VOL.Native;

internal enum FilterIdentifier : ushort
{
    NA = 0,
    Deflate = 1,
    Shuffle = 2,
    Fletcher32 = 3,
    Szip = 4,
    Nbit = 5,
    ScaleOffset = 6
}