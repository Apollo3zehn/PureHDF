namespace PureHDF.VOL.Native;

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
};