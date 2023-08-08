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
        return new ChunkInfo(Dataset.Layout.Address, ChunkByteSize, 0);
    }

    protected override ChunkInfo GetWriteChunkInfo(ulong[] chunkIndices, uint chunkSize)
    {
        throw new NotImplementedException();
    }
}