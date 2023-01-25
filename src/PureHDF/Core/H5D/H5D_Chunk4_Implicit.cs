namespace PureHDF
{
    internal class H5D_Chunk4_Implicit : H5D_Chunk4
    {
        public H5D_Chunk4_Implicit(H5Dataset dataset, DataLayoutMessage4 layout, H5DatasetAccess datasetAccess) :
            base(dataset, layout, datasetAccess)
        {
            //
        }

        protected override ChunkInfo GetChunkInfo(ulong[] chunkIndices)
        {
            var chunkIndex = chunkIndices.ToLinearIndexPrecomputed(DownMaxChunkCounts);
            var chunkOffset = chunkIndex * ChunkByteSize;

            return new ChunkInfo(Dataset.InternalDataLayout.Address + chunkOffset, ChunkByteSize, 0);
        }
    }
}
