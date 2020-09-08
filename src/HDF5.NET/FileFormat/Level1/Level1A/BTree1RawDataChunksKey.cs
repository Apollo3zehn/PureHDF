using System.Collections.Generic;

namespace HDF5.NET
{
    public class BTree1RawDataChunksKey : BTree1Key
    {
        #region Constructors

        public BTree1RawDataChunksKey(H5BinaryReader reader, int dimensionality) : base(reader)
        {
            this.ChunkSize = reader.ReadUInt32();
            this.FilterMask = reader.ReadUInt32();

            this.ChunkOffsets = new List<ulong>(dimensionality);

            for (int i = 0; i < dimensionality; i++)
            {
                this.ChunkOffsets[i] = reader.ReadUInt64();
            }
        }

        #endregion

        #region Properties

        public uint ChunkSize { get; set; }
        public uint FilterMask { get; set; }
        public List<ulong> ChunkOffsets { get; set; }

        #endregion
    }
}
