using PureHDF.VOL.Native;

namespace PureHDF;

internal static class DataspaceExtensions
{
    public static ulong[] GetDims(this DataspaceMessage dataspace)
    {
        return dataspace.Type switch
        {
            DataspaceType.Scalar => new ulong[] { 1 },
            DataspaceType.Simple => dataspace.Dimensions,
            _ => throw new Exception($"The dataspace type '{dataspace.Type}' is not supported.")
        };
    }

    public static ulong GetTotalElementCount(this DataspaceMessage dataspace)
    {
        return dataspace.Type switch
        {
            DataspaceType.Null => 0,
            DataspaceType.Scalar => 1,
            DataspaceType.Simple => dataspace.Dimensions.Aggregate(1UL, (product, dim) => product * dim),
            _ => throw new Exception($"The dataspace type '{dataspace.Type}' is not supported.")
        };
    }
}