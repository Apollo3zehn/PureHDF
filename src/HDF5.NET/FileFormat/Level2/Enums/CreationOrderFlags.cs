namespace HDF5.NET
{
    [Flags]
    internal enum CreationOrderFlags : byte
    {
        TrackCreationOrder = 1,
        IndexCreationOrder = 2
    }
}
