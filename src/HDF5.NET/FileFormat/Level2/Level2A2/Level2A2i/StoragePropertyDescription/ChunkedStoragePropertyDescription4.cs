using System;

namespace HDF5.NET
{
    public class ChunkedStoragePropertyDescription4 : StoragePropertyDescription
    {
        #region Constructors

        public ChunkedStoragePropertyDescription4(H5BinaryReader reader, Superblock superblock) : base(reader)
        {
            // flags
            this.Flags = (ChunkedStoragePropertyFlags)reader.ReadByte();

            // dimensionality
            this.Dimensionality = reader.ReadByte();

            // dimension size encoded length
            this.DimensionSizeEncodedLength = reader.ReadByte();

            // dimension sizes
            this.DimensionSizes = new ulong[this.Dimensionality - 1];

            for (uint i = 0; i < this.Dimensionality - 1; i++)
            {
                this.DimensionSizes[i] = H5Utils.ReadUlong(reader, this.DimensionSizeEncodedLength);
            }

            // chunk indexing type
            this.ChunkIndexingType = (ChunkIndexingType)reader.ReadByte();

            // indexing type information
            this.IndexingTypeInformation = this.ChunkIndexingType switch
            {
                ChunkIndexingType.SingleChunk       => new SingleChunkIndexingInformation(reader, superblock),
                ChunkIndexingType.Implicit          => new ImplicitIndexingInformation(reader),
                ChunkIndexingType.FixedArray        => new FixedArrayIndexingInformation(reader),
                ChunkIndexingType.ExtensibleArray   => new ExtensibleArrayIndexingInformation(reader),
                ChunkIndexingType.BTree2            => new BTree2IndexingInformation(reader),
                _ => throw new NotSupportedException($"The chunk indexing type '{this.ChunkIndexingType}' is not supported.")
            };

            // address
            this.Address = superblock.ReadOffset(reader);
        }

        #endregion

        #region Properties

        public ChunkedStoragePropertyFlags Flags { get; set; }
        public byte Dimensionality { get; set; }
        public byte DimensionSizeEncodedLength { get; set; }
        public ulong[] DimensionSizes { get; set; }
        public ChunkIndexingType ChunkIndexingType { get; set; }
        public IndexingInformation IndexingTypeInformation { get; set; }
        public ulong Address { get; set; }

        #endregion
    }
}
