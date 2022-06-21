using System;

namespace HDF5.NET
{
    internal class DataLayoutMessage3 : DataLayoutMessage
    {
        #region Fields

        private byte _version;

        #endregion

        #region Constructors

        internal DataLayoutMessage3(H5BinaryReader reader, Superblock superblock, byte version) : base(reader)
        {
            // version
            Version = version;

            // layout class
            LayoutClass = (LayoutClass)reader.ReadByte();

            // storage property description
            Properties = (Version, LayoutClass) switch
            {
                (_, LayoutClass.Compact)        => new CompactStoragePropertyDescription(reader),
                (_, LayoutClass.Contiguous)     => new ContiguousStoragePropertyDescription(reader, superblock),
                (3, LayoutClass.Chunked)        => new ChunkedStoragePropertyDescription3(reader, superblock),
                (4, LayoutClass.Chunked)        => new ChunkedStoragePropertyDescription4(reader, superblock),
                (4, LayoutClass.VirtualStorage) => new VirtualStoragePropertyDescription(reader, superblock),
                _ => throw new NotSupportedException($"The layout class '{LayoutClass}' is not supported for the data layout message version '{Version}'.")
            };

            // address
            Address = Properties.Address;
        }

        #endregion

        #region Properties

        public byte Version
        {
            get
            {
                return _version;
            }
            set
            {
                if (!(3 <= value && value <= 4))
                    throw new FormatException($"Only version 3 and version 4 instances of type {nameof(DataLayoutMessage3)} are supported.");

                _version = value;
            }
        }

        public StoragePropertyDescription Properties { get; set; }


        #endregion
    }
}
