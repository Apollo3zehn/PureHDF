using PureHDF.VFD;

namespace PureHDF.VOL.Native;

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

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (Chunked4.IsDirty)
        {
            // Patched in the copy kept at encoding rather than in the file, then written whole: the
            // storage properties changed, and with them the header's checksum, which is computed from
            // the copy so that nothing is read from the file.
            var (objectHeaderAddress, objectHeader) = WriteContext.DatasetInfoToObjectHeaderMap[Dataset];
            var origin = (long)WriteContext.Driver.BaseAddress + objectHeaderAddress;

            using (var patchDriver = new H5SpanWriteDriver(objectHeader, origin))
            {
                patchDriver.Seek(Chunked4.EncodeAddress, SeekOrigin.Begin);
                Chunked4.Encode(patchDriver);
            }

            ChecksummedStructure.UpdateChecksum(objectHeader);

            WriteContext.Driver.SeekRelativeToBaseAddress(objectHeaderAddress);
            WriteContext.Driver.Write(objectHeader);
        }
    }

    protected abstract ChunkInfo GetActualWriteChunkInfo(ulong chunkIndex, uint chunkSize, uint filterMask);

    protected override ChunkInfo GetWriteChunkInfo(ulong chunkIndex, uint chunkSize, uint filterMask)
    {
        if (WriteChunkInfos[chunkIndex].Address == Superblock.UndefinedAddress)
        {
            var chunkInfo = GetActualWriteChunkInfo(chunkIndex, chunkSize, filterMask);
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