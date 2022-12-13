namespace HDF5.NET
{
    [Flags]
    internal enum DataspaceMessageFlags : byte
    {
        DimensionMaxSizes = 1,
        PermuationIndices = 2
    }
}
