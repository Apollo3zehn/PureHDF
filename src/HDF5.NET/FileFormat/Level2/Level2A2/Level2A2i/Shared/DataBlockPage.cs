using System;

namespace HDF5.NET
{
    public class DataBlockPage
    {
        #region Constructors

        public DataBlockPage(H5BinaryReader reader,
                                       Superblock superblock,
                                       ulong elementsPerPage,
                                       ClientID clientID,
                                       uint chunkSizeLength)
        {
            // elements
            this.Elements = ArrayIndexUtils.ReadElements(reader, superblock, elementsPerPage, clientID, chunkSizeLength);

            // checksum
            this.Checksum = reader.ReadUInt32();
        }

        #endregion

        #region Properties

        public DataBlockElement[] Elements { get; }

        public ulong Checksum { get; }

        #endregion
    }
}
