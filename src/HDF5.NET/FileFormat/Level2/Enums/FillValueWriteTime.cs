namespace HDF5.NET
{
    internal enum FillValueWriteTime : byte
    {
        OnAllocation = 0,
        Never = 1,
        IfSetByUser = 2
    }
}
