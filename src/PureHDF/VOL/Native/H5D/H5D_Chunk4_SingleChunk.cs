namespace PureHDF;

internal class H5Dataset_Chunk_Single_Chunk4 : H5D_Chunk4
{
    public H5Dataset_Chunk_Single_Chunk4(NativeReadContext readContext, NativeWriteContext writeContext, DatasetInfo dataset, DataLayoutMessage4 layout, H5DatasetAccess datasetAccess) :
        base(readContext, writeContext, dataset, layout, datasetAccess)
    {
        //
    }

    protected override ChunkInfo GetReadChunkInfo(ulong[] chunkIndices)
    {
        var single = (SingleChunkIndexingInformation)Chunked4.IndexingInformation;

        var chunkSize = single.FilteredChunkSize == 0
            ? ChunkByteSize
            : single.FilteredChunkSize;

        return new ChunkInfo(Chunked4.Address, chunkSize, single.ChunkFilters);
    }

    protected override ChunkInfo GetWriteChunkInfo(ulong[] chunkIndices, uint chunkSize, uint filterMask)
    {
        /* see also H5D__single_idx_insert (H5DSingle.c) */
        Chunked4.Address = (ulong)WriteContext.FreeSpaceManager.Allocate(chunkSize);
        
        var single = (SingleChunkIndexingInformation)Chunked4.IndexingInformation;
        single.FilteredChunkSize = chunkSize;

        return new ChunkInfo(Chunked4.Address, chunkSize, filterMask);
    }
}