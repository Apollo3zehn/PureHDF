namespace HDF5.NET
{
    internal class H5Dataset_Chunk_Single_Chunk4 : H5D_Chunk4
    {
        public H5Dataset_Chunk_Single_Chunk4(H5Dataset dataset, DataLayoutMessage4 layout, H5DatasetAccess datasetAccess) : 
            base(dataset, layout, datasetAccess)
        {
            //
        }

        protected override ChunkInfo GetChunkInfo(ulong[] chunkIndices)
        {
            return new ChunkInfo(this.Dataset.DataLayout.Address, this.ChunkByteSize, 0);
        }
    }
}
