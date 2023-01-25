namespace HDF5.NET
{
    internal class ChunkedStoragePropertyDescription4 : ChunkedStoragePropertyDescription
    {
        #region Constructors

        public ChunkedStoragePropertyDescription4(H5Context context)
        {
            var (reader, superblock) = context;

            // flags
            Flags = (ChunkedStoragePropertyFlags)reader.ReadByte();

            // rank
            Rank = reader.ReadByte();

            // dimension size encoded length
            DimensionSizeEncodedLength = reader.ReadByte();

            // dimension sizes
            DimensionSizes = new ulong[Rank];

            for (uint i = 0; i < Rank; i++)
            {
                DimensionSizes[i] = H5Utils.ReadUlong(reader, DimensionSizeEncodedLength);
            }

            // chunk indexing type
            ChunkIndexingType = (ChunkIndexingType)reader.ReadByte();

            // indexing type information
            IndexingTypeInformation = ChunkIndexingType switch
            {
                ChunkIndexingType.SingleChunk => new SingleChunkIndexingInformation(context, Flags),
                ChunkIndexingType.Implicit => new ImplicitIndexingInformation(),
                ChunkIndexingType.FixedArray => new FixedArrayIndexingInformation(reader),
                ChunkIndexingType.ExtensibleArray => new ExtensibleArrayIndexingInformation(reader),
                ChunkIndexingType.BTree2 => new BTree2IndexingInformation(reader),
                _ => throw new NotSupportedException($"The chunk indexing type '{ChunkIndexingType}' is not supported.")
            };

            // address
            Address = superblock.ReadOffset(reader);
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
