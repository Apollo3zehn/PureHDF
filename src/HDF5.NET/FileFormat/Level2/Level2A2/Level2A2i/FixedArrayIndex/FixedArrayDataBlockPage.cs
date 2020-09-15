using System;

namespace HDF5.NET
{
    public class FixedArrayDataBlockPage
    {
        #region Constructors

        public FixedArrayDataBlockPage(H5BinaryReader reader,
                                       Superblock superblock,
                                       ulong elementsPerPage,
                                       FixedArrayClientID clientID,
                                       uint chunkSizeLength)
        {
            // elements
            this.Elements = new FixedArrayDataBlockElement[elementsPerPage];

            switch (clientID)
            {
                case FixedArrayClientID.NonFilteredDatasetChunks:

                    for (ulong i = 0; i < elementsPerPage; i++)
                    {
                        this.Elements[i] = new FixedArrayDataBlockElement()
                        {
                            Address = superblock.ReadOffset(reader)
                        };
                    }

                    break;

                case FixedArrayClientID.FilteredDatasetChunks:

                    for (ulong i = 0; i < elementsPerPage; i++)
                    {
                        this.Elements[i] = new FixedArrayDataBlockElement()
                        {
                            Address = superblock.ReadOffset(reader),
                            ChunkSize = (uint)H5Utils.ReadUlong(reader, chunkSizeLength),
                            FilterMask = reader.ReadUInt32()
                        };
                    }

                    break;

                default:
                    throw new Exception($"Client ID '{clientID}' is not supported.");
            }

            // checksum
            this.Checksum = reader.ReadUInt32();
        }

        #endregion

        #region Properties
        public FixedArrayDataBlockElement[] Elements { get; }

        public ulong Checksum { get; }

        #endregion
    }
}
