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

        var header = WriteContext.DatasetInfoToObjectHeaderMap[Dataset];

        AllocateIndex();

        // A header allocated late gets its address now that it is complete, unless something named it before.
        var headerAddress = WriteContext.Writer.AllocateHeader(header);
        var isPatched = PatchLayout(header);

        // A header written at encoding is final unless its layout changed since; one held back for its
        // chunk is written now, whether or not the chunk ever was. A shared object's header is written at
        // the end, from the copy patched here.
        if (!header.IsShared && (isPatched || !header.IsWritten))
        {
            WriteContext.Driver.SeekRelativeToBaseAddress((long)headerAddress);
            WriteContext.Driver.Write(header.Header);
        }
    }

    /// <summary>
    /// Encodes the layout into the header's kept copy again if it changed since the header was encoded, and
    /// updates the checksum.
    /// </summary>
    /// <remarks>
    /// Patched in the copy rather than in the file: the header's checksum is computed from the copy instead
    /// of from bytes read back.
    /// </remarks>
    /// <returns>Whether the layout changed.</returns>
    private bool PatchLayout(KeptHeader header)
    {
        if (!Chunked4.IsDirty)
            return false;

        var origin = (long)WriteContext!.Driver.BaseAddress + (long)header.EncodedAt;

        // Encoding the layout clears its dirty flag.
        using (var patchDriver = new H5SpanWriteDriver(header.Header, origin))
        {
            patchDriver.Seek(Chunked4.EncodeAddress, SeekOrigin.Begin);
            Chunked4.Encode(patchDriver);
        }

        ChecksummedStructure.UpdateChecksum(header.Header);

        return true;
    }

    /// <summary>
    /// Allocates an index whose size is known up front but which is allocated only once the chunks are
    /// written, so that it is no gap until then. Marks the layout dirty when it does.
    /// </summary>
    protected virtual void AllocateIndex()
    {
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