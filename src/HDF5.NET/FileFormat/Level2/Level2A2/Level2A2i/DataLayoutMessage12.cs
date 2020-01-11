using System;
using System.IO;

namespace HDF5.NET
{
    public class DataLayoutMessage12 : DataLayoutMessage
    {
        #region Fields

        private byte _version;

        #endregion

        #region Constructors

        internal DataLayoutMessage12(BinaryReader reader, Superblock superblock, byte version) : base(reader)
        {
            // version
            this.Version = version;

            // dimensionality
            this.Dimensionality = reader.ReadByte();

            // layout class
            this.LayoutClass = (LayoutClass)reader.ReadByte();

            // reserved
            reader.ReadBytes(5);

            // data address
            this.DataAddress = this.LayoutClass switch
            {
                LayoutClass.Compact     => ulong.MaxValue, // invalid address
                LayoutClass.Contiguous  => superblock.ReadOffset(),
                LayoutClass.Chunked     => superblock.ReadOffset(),
                _ => throw new NotSupportedException($"The layout class '{this.LayoutClass}' is not supported.")
            };

            // dimension sizes
            this.DimensionSizes = new uint[this.Dimensionality];

            for (int i = 0; i < this.Dimensionality; i++)
            {
                this.DimensionSizes[i] = reader.ReadUInt32();
            }

            // dataset element size
            if (this.LayoutClass == LayoutClass.Chunked)
                this.DatasetElementSize = reader.ReadUInt32();

            // compact data size
            if (this.LayoutClass == LayoutClass.Compact)
            {
                this.CompactDataSize = reader.ReadUInt32();
                this.CompactData = reader.ReadBytes((int)this.CompactDataSize);
            }
            else
            {
                this.CompactData = new byte[0];
            }
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
                if (!(1 <= value && value <= 2))
                    throw new FormatException($"Only version 1 and version 2 instances of type {nameof(DataLayoutMessage12)} are supported.");

                _version = value;
            }
        }

        public byte Dimensionality { get; set; }
        public ulong DataAddress { get; set; }
        public uint[] DimensionSizes { get; set; }
        public uint DatasetElementSize { get; set; }
        public uint CompactDataSize { get; set; }
        public byte[] CompactData { get; set; }

        #endregion
    }
}
