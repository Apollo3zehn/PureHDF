namespace PureHDF
{
    internal class SingleChunkIndexingInformation : IndexingInformation
    {
        #region Constructors

        public SingleChunkIndexingInformation(H5Context context, ChunkedStoragePropertyFlags flags)
        {
            if (flags.HasFlag(ChunkedStoragePropertyFlags.SINGLE_INDEX_WITH_FILTER))
            {
                var (reader, superblock) = context;

                // filtered chunk size
                FilteredChunkSize = superblock.ReadLength(reader);

                // chunk filters
                ChunkFilters = reader.ReadUInt32();
            }
        }

        #endregion

        #region Properties

        public ulong FilteredChunkSize { get; set; }
        public uint ChunkFilters { get; set; }

        #endregion
    }
}
