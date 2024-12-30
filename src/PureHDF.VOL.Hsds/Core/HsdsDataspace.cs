using Hsds.Api.V2_0;

namespace PureHDF.VOL.Hsds;

internal class HsdsDataspace : IH5Dataspace
{
    public HsdsDataspace(ShapeType shape)
    {
        Type = shape.Class switch
        {
            "H5S_NULL" => H5DataspaceType.Null,
            "H5S_SCALAR" => H5DataspaceType.Scalar,
            "H5S_SIMPLE" => H5DataspaceType.Simple,
            _ => throw new Exception($"Unknown dataspace type {shape.Class}.")
        };

        Rank = (byte)(shape.Dims is null
            ? 0
            : shape.Dims.Count);

        Dimensions = shape.Dims is null
            ? Array.Empty<ulong>()
            : shape.Dims
                .Select(dim => (ulong)dim)
                .ToArray();

        MaxDimensions = shape.Maxdims is null
            ? Array.Empty<ulong>()
            : shape.Maxdims
                .Select(dim => (ulong)dim)
                .ToArray();
    }

    public byte Rank { get; }

    public H5DataspaceType Type { get; }

    public ulong[] Dimensions { get; }

    public ulong[] MaxDimensions { get; }
}