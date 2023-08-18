namespace PureHDF;

internal abstract class H5D_Chunk4 : H5D_Chunk
{
    #region Constructors

    public H5D_Chunk4(
        NativeReadContext readContext, 
        NativeWriteContext writeContext, 
        DatasetInfo dataset, 
        DataLayoutMessage4 layout, 
        H5DatasetAccess datasetAccess,
        H5DatasetCreation datasetCreation)
        : base(readContext, writeContext, dataset, datasetAccess, datasetCreation)
    {
        Layout = layout;
        Chunked4 = (ChunkedStoragePropertyDescription4)layout.Properties;
    }

    #endregion

    #region Properties

    public DataLayoutMessage4 Layout { get; }

    public ChunkedStoragePropertyDescription4 Chunked4 { get; }

    protected ChunkInfo[] WriteChunkInfos { get; private set; } = default!;

    #endregion

    #region Methods

    public override void Initialize()
    {
        base.Initialize();

        if (WriteContext is not null)
        {
            WriteChunkInfos = new ChunkInfo[TotalChunkCount];

            for (int i = 0; i < WriteChunkInfos.Length; i++)
            {
                WriteChunkInfos[i].Address = Superblock.UndefinedAddress;
            }
        }
    }

    protected abstract ChunkInfo GetActualWriteChunkInfo(ulong[] chunkIndices, uint chunkSize, uint filterMask);

    protected override ChunkInfo GetWriteChunkInfo(ulong[] chunkIndices, uint chunkSize, uint filterMask)
    {
        var chunkIndex = chunkIndices.ToLinearIndexPrecomputed(DownMaxChunkCounts);

        if (WriteChunkInfos[chunkIndex].Address == Superblock.UndefinedAddress)
        {
            var chunkInfo = GetActualWriteChunkInfo(chunkIndices, chunkSize, filterMask);
            WriteChunkInfos[chunkIndex] = chunkInfo;

            return chunkInfo;
        }        

        else
        {
            throw new Exception("Chunks can only be written once. Consider increasing the size of the chunk cache to avoid chunks written to disk too early.");
        }
    }

    protected override ulong[] GetRawChunkDims()
    {
        return Chunked4
            .DimensionSizes
            .ToArray();
    }

    #endregion
}