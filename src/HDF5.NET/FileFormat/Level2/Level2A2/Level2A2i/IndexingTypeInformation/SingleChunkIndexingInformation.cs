namespace HDF5.NET
{
    internal class SingleChunkIndexingInformation : IndexingInformation
    {
        #region Constructors

        public SingleChunkIndexingInformation(H5BinaryReader reader, Superblock superblock, ChunkedStoragePropertyFlags flags) : base(reader)
        {
            if (flags.HasFlag(ChunkedStoragePropertyFlags.SINGLE_INDEX_WITH_FILTER))
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
