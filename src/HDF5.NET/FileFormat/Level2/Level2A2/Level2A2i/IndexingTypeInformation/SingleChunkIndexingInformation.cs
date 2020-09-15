namespace HDF5.NET
{
    public class SingleChunkIndexingInformation : IndexingInformation
    {
        #region Constructors

        public SingleChunkIndexingInformation(H5BinaryReader reader, Superblock superblock, ChunkedStoragePropertyFlags flags) : base(reader)
        {
            if (flags.HasFlag(ChunkedStoragePropertyFlags.DONT_FILTER_PARTIAL_BOUND_CHUNKS))
            {
                // filtered chunk size
                this.FilteredChunkSize = superblock.ReadLength(reader);

                // chunk filters
                this.ChunkFilters = reader.ReadUInt32();
            }
        }

        #endregion

        #region Properties

        public ulong FilteredChunkSize { get; set; }
        public uint ChunkFilters { get; set; }

        #endregion
    }
}
