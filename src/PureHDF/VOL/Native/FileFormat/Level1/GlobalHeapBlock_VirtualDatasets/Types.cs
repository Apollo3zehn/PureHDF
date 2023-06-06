internal readonly record struct LinearIndexResult(
    bool Success, 
    ulong LinearIndex, 
    ulong MaxCount);

internal readonly record struct CoordinatesResult(
    ulong[] Coordinates, 
    ulong MaxCount);

internal enum SelectionType : uint
{
    H5S_SEL_NONE = 0,
    H5S_SEL_POINTS = 1,
    H5S_SEL_HYPER = 2,
    H5S_SEL_ALL = 3,
    H5S_SEL_POINTS_SPECIAL_HANDLING = uint.MaxValue
}