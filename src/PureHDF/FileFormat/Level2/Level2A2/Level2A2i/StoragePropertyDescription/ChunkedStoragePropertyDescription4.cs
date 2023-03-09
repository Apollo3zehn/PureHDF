namespace PureHDF
{
    internal class ChunkedStoragePropertyDescription4 : ChunkedStoragePropertyDescription
    {
        #region Constructors

        public ChunkedStoragePropertyDescription4(H5Context context)
        {
            var (driver, superblock) = context;

            // flags
            Flags = (ChunkedStoragePropertyFlags)driver.ReadByte();

            // rank
            Rank = driver.ReadByte();

            // dimension size encoded length
            DimensionSizeEncodedLength = driver.ReadByte();

            // dimension sizes
            DimensionSizes = new ulong[Rank];

            for (uint i = 0; i < Rank; i++)
            {
                DimensionSizes[i] = Utils.ReadUlong(driver, DimensionSizeEncodedLength);
            }

            // chunk indexing type
            ChunkIndexingType = (ChunkIndexingType)driver.ReadByte();

            // indexing type information
            IndexingTypeInformation = ChunkIndexingType switch
            {
                ChunkIndexingType.SingleChunk => new SingleChunkIndexingInformation(context, Flags),
                ChunkIndexingType.Implicit => new ImplicitIndexingInformation(),
                ChunkIndexingType.FixedArray => new FixedArrayIndexingInformation(driver),
                ChunkIndexingType.ExtensibleArray => new ExtensibleArrayIndexingInformation(driver),
                ChunkIndexingType.BTree2 => new BTree2IndexingInformation(driver),
                _ => throw new NotSupportedException($"The chunk indexing type '{ChunkIndexingType}' is not supported.")
            };

            // address
            Address = superblock.ReadOffset(driver);
        }

        #endregion

        #region Properties

        public ChunkedStoragePropertyFlags Flags { get; set; }
        public byte DimensionSizeEncodedLength { get; set; }
        public ulong[] DimensionSizes { get; set; }
        public ChunkIndexingType ChunkIndexingType { get; set; }
        public IndexingInformation IndexingTypeInformation { get; set; }

        #endregion
    }
}
