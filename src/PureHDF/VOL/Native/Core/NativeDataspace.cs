namespace PureHDF.VOL.Native;

internal class NativeDataspace : IH5Dataspace
{
    #region Fields

    private readonly DataspaceMessage _dataspace;

    #endregion

    #region Properties

    public byte Rank => _dataspace.Rank;

    public H5DataspaceType Type => (H5DataspaceType)_dataspace.Type;

    public ulong[] Dimensions => _dataspace.DimensionSizes.ToArray();

    public ulong[] MaxDimensions => _dataspace.DimensionMaxSizes.ToArray();

    #endregion

    #region Constructors

    internal NativeDataspace(DataspaceMessage dataspace)
    {
        _dataspace = dataspace;
    }

    #endregion
}