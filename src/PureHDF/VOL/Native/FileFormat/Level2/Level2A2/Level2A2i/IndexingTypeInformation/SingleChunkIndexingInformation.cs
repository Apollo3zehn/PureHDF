namespace PureHDF.VOL.Native;

internal class SingleChunkIndexingInformation : IndexingInformation
{
    #region Constructors

    public SingleChunkIndexingInformation(H5Context context, ChunkedStoragePropertyFlags flags)
    {
        if (flags.HasFlag(ChunkedStoragePropertyFlags.SINGLE_INDEX_WITH_FILTER))
        {
            var (driver, superblock) = context;

            // filtered chunk size
            FilteredChunkSize = superblock.ReadLength(driver);

            // chunk filters
            ChunkFilters = driver.ReadUInt32();
        }
    }

    #endregion

    #region Properties

    public ulong FilteredChunkSize { get; set; }
    public uint ChunkFilters { get; set; }

    #endregion
}