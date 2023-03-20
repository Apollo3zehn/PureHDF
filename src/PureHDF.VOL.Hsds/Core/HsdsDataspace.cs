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
}