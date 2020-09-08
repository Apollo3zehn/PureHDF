using System;

namespace HDF5.NET
{
    public class DataLayoutMessage34 : DataLayoutMessage
    {
        #region Fields

        private byte _version;

        #endregion

        #region Constructors

        internal DataLayoutMessage34(H5BinaryReader reader, Superblock superblock, byte version) : base(reader)
        {
            // version
            this.Version = version;

            // layout class
            this.LayoutClass = (LayoutClass)reader.ReadByte();

            // storage property description
            this.Properties = (this.Version, this.LayoutClass) switch
            {
                (_, LayoutClass.Compact)        => new CompactStoragePropertyDescription(reader),
                (_, LayoutClass.Contiguous)     => new ContiguousStoragePropertyDescription(reader, superblock),
                (3, LayoutClass.Chunked)        => new ChunkedStoragePropertyDescription3(reader, superblock),
                (4, LayoutClass.Chunked)        => new ChunkedStoragePropertyDescription4(reader, superblock),
                (4, LayoutClass.VirtualStorage) => new VirtualStoragePropertyDescription(reader, superblock),
                _ => throw new NotSupportedException($"The layout class '{this.LayoutClass}' is not supported for the data layout message version '{this.Version}'.")
            };
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
                    throw new FormatException($"Only version 3 and version 4 instances of type {nameof(DataLayoutMessage34)} are supported.");

                _version = value;
            }
        }

        public StoragePropertyDescription Properties { get; set; }


        #endregion
    }
}
