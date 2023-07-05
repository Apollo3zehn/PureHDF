namespace PureHDF.VOL.Native;

[Flags]
internal enum DataspaceMessageFlags : byte
{
    None = 0,
    DimensionMaxSizes = 1,
    PermuationIndices = 2
}