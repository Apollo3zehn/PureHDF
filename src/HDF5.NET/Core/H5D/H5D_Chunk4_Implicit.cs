namespace HDF5.NET
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
            var chunkIndex = chunkIndices.ToLinearIndexPrecomputed(this.DownMaxChunkCounts);
            var chunkOffset = chunkIndex * this.ChunkByteSize;

            return new ChunkInfo(this.Dataset.InternalDataLayout.Address + chunkOffset, this.ChunkByteSize, 0);
        }
    }
}
