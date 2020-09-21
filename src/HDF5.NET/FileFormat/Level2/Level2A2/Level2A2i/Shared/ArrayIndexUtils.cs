using System;

namespace HDF5.NET
{
    public static class ArrayIndexUtils
    {
        public static DataBlockElement[] ReadElements(H5BinaryReader reader,
                                                      Superblock superblock,
                                                      ulong elementsCount,
                                                      ClientID clientID,
                                                      uint chunkSizeLength)
        {
            var elements = new DataBlockElement[elementsCount];

            switch (clientID)
            {
                case ClientID.NonFilteredDatasetChunks:

                    for (ulong i = 0; i < elementsCount; i++)
                    {
                        elements[i] = new DataBlockElement()
                        {
                            Address = superblock.ReadOffset(reader)
                        };
                    }

                    break;

                case ClientID.FilteredDatasetChunks:

                    for (ulong i = 0; i < elementsCount; i++)
                    {
                        elements[i] = new DataBlockElement()
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

            return elements;
        }
    }
}
