namespace PureHDF;

/* Must be a normal class because of being a key in WriteContext.DatasetInfoToObjectHeaderMap! 
 * Otherwise the test CanWrite_Chunked_single_chunk_filtered_Deferred will fail 
 */
internal class DatasetInfo(
    DataspaceMessage Space,
    DatatypeMessage Type,
    DataLayoutMessage Layout,
    FillValueMessage FillValue,
    FilterPipelineMessage? FilterPipeline,
    ExternalFileListMessage? ExternalFileList
)
{
    public DataspaceMessage Space { get; } = Space;

    public DatatypeMessage Type { get; } = Type;

    public DataLayoutMessage Layout { get; } = Layout;

    public FillValueMessage FillValue { get; } = FillValue;

    public FilterPipelineMessage? FilterPipeline { get; } = FilterPipeline;

    public ExternalFileListMessage? ExternalFileList { get; } = ExternalFileList;

    internal ulong[] GetDatasetDims()
    {
        return Space.Type switch
        {
            DataspaceType.Scalar => new ulong[] { 1 },
            DataspaceType.Simple => Space.Dimensions,
            _ => throw new Exception($"Unsupported data space type '{Space.Type}'.")
        };
    }
};