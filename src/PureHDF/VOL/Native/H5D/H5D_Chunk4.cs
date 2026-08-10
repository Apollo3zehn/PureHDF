using System.Buffers;

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
            // Encode ChunkedStoragePropertyDescriptions
            WriteContext.Driver.Seek(Chunked4.EncodeAddress, SeekOrigin.Begin);
            Chunked4.Encode(WriteContext.Driver);

            // Update object header checksum
            var (objectHeaderAddress, length) = WriteContext.DatasetInfoToObjectHeaderMap[Dataset];
            var lengthWithoutChecksum = length - sizeof(uint);
            using var memorOwner = MemoryPool<byte>.Shared.Rent(lengthWithoutChecksum);
            var checksumData = memorOwner.Memory.Slice(0, lengthWithoutChecksum);

            WriteContext.Driver.SeekRelativeToBaseAddress(objectHeaderAddress);

            // SYNC SURFACE: this runs from Dispose on the synchronous writer, while driver.Read is
            // async now. The ValueTask was previously discarded outright - and because the enclosing
            // method is not async there is no CS4014 warning - so the checksum was computed over
            // uninitialized pooled memory, producing a file h5dump rejects. AsTask() is required:
            // blocking directly on an IValueTaskSource-backed ValueTask is unsupported.
            WriteContext.Driver.Read(checksumData).AsTask().GetAwaiter().GetResult();

            var checksum = ChecksumUtils.JenkinsLookup3(checksumData.Span);

            // Absolute seek: the async read leaves a BufferedFileStream's read buffer in a state
            // where the sync Write below can fail inside its internal FlushRead.
            WriteContext.Driver.SeekRelativeToBaseAddress((long)objectHeaderAddress + lengthWithoutChecksum);

            WriteContext.Driver.Write(checksum);
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