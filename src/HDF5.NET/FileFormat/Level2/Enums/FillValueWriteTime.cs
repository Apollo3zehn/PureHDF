namespace HDF5.NET
{
    public enum FillValueWriteTime : byte
    {
        OnAllocation = 0,
        Never = 1,
        FillValueWrittenIfSetByUser = 2
    }
}
