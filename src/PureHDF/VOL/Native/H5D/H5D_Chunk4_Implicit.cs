namespace PureHDF.VOL.Native;

internal class H5D_Chunk4_Implicit : H5D_Chunk4
{
    public H5D_Chunk4_Implicit(
        NativeReadContext readContext,
        NativeWriteContext writeContext,
        DatasetInfo dataset,
        DataLayoutMessage4 layout,
        H5DatasetAccess datasetAccess,
        H5DatasetCreation datasetCreation) :
        base(readContext, writeContext, dataset, layout, datasetAccess, datasetCreation)
    {
        //
    }

    protected override ChunkInfo GetReadChunkInfo(ulong[] chunkIndices)
    {
        var chunkIndex = chunkIndices.ToLinearIndexPrecomputed(DownMaxChunkCounts);
        var chunkOffset = chunkIndex * ChunkByteSize;

        return new ChunkInfo(Chunked4.Address + chunkOffset, ChunkByteSize, 0);
    }

    protected override ChunkInfo GetActualWriteChunkInfo(ulong chunkIndex, uint chunkSize, uint filterMask)
    {
        var chunkOffset = chunkIndex * ChunkByteSize;

        return new ChunkInfo(Chunked4.Address + chunkOffset, ChunkByteSize, 0);
    }
}