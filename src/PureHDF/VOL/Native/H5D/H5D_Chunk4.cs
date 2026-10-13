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

        if (WriteContext is null)
            return;

        var (objectHeaderAddress, objectHeader, isWritten) = WriteContext.DatasetInfoToObjectHeaderMap[Dataset];

        // Read before patching, which encodes the layout and so clears it.
        var isDirty = Chunked4.IsDirty;

        if (isDirty)
        {
            // Patched in the copy kept at encoding rather than in the file: the storage properties
            // changed, and with them the header's checksum, which is computed from the copy so that
            // nothing is read from the file.
            var origin = (long)WriteContext.Driver.BaseAddress + objectHeaderAddress;

            using (var patchDriver = new H5SpanWriteDriver(objectHeader, origin))
            {
                patchDriver.Seek(Chunked4.EncodeAddress, SeekOrigin.Begin);
                Chunked4.Encode(patchDriver);
            }

            ChecksummedStructure.UpdateChecksum(objectHeader);
        }

        // A header written at encoding is final unless its layout changed since; one held back for its
        // chunk is written now, whether or not the chunk ever was.
        if (isDirty || !isWritten)
        {
            WriteContext.Driver.SeekRelativeToBaseAddress(objectHeaderAddress);
            WriteContext.Driver.Write(objectHeader);
        }
    }

    /// <remarks>
    /// An unfiltered chunk is stored at its full size, so its address does not depend on its content: an
    /// implicit index fixes it when the layout is created, and a single chunk gets it on its first write.
    /// </remarks>
    protected override bool TryGetWriteThroughAddress(ulong chunkIndex, out ulong address)
    {
        if (Dataset.FilterPipeline is not null)
        {
            address = default;
            return false;
        }

        var chunkInfo = WriteChunkInfos[chunkIndex];

        if (chunkInfo.Address == Superblock.UndefinedAddress)
            chunkInfo = GetWriteChunkInfo(chunkIndex, (uint)ChunkByteSize, filterMask: 0);

        address = chunkInfo.Address;
        return true;
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