namespace PureHDF;

internal record DatasetInfo(
    DataspaceMessage Space,
    DatatypeMessage Type,
    DataLayoutMessage Layout,
    FillValueMessage FillValue,
    FilterPipelineMessage? FilterPipeline,
    ExternalFileListMessage? ExternalFileList
)
{
    internal ulong[] GetDatasetDims()
    {
        return Space.Type switch
        {
            DataspaceType.Scalar => new ulong[] { 1 },
            DataspaceType.Simple => Space.DimensionSizes,
            _ => throw new Exception($"Unsupported data space type '{Space.Type}'.")
        };
    }
};