using Hsds.Api;

namespace PureHDF.VOL.Hsds;

internal class HsdsDataspace : IH5Dataspace
{
    public HsdsDataspace(byte rank, H5DataspaceType type, ulong[] dimensions, ulong[] maxDimensions)
    {
        Rank = rank;
        Type = type;
        Dimensions = dimensions;
        MaxDimensions = maxDimensions;
    }

    public byte Rank { get; }

    public H5DataspaceType Type { get; }

    public ulong[] Dimensions { get; }

    public ulong[] MaxDimensions { get; }

    public static IH5Dataspace FromGetDatasetResponseShapeType(GetDatasetResponseShapeType shape)
    {
        var rank = (byte)shape.Dims.Count;

        var type = shape.Class switch
        {
            "H5S_NULL" => H5DataspaceType.Null,
            "H5S_SCALAR" => H5DataspaceType.Scalar,
            "H5S_SIMPLE" => H5DataspaceType.Simple,
            _ => throw new Exception($"Unknown dataspace type {shape.Class}.")
        };

        var dimensions = shape.Dims
            .Select(dim => (ulong)dim)
            .ToArray();

        var maxDimensions = shape.Maxdims
            .Select(dim => (ulong)dim)
            .ToArray();

        return new HsdsDataspace(rank, type, dimensions, maxDimensions);
    }
}