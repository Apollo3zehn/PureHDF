namespace PureHDF
{
    internal class DataLayoutMessage12 : DataLayoutMessage
    {
        #region Fields

        private byte _version;

        #endregion

        #region Constructors

        internal DataLayoutMessage12(H5Context context, byte version)
        {
            var (reader, superblock) = context;

            // version
            Version = version;

            // rank
            Rank = reader.ReadByte();

            // layout class
            LayoutClass = (LayoutClass)reader.ReadByte();

            // reserved
            reader.ReadBytes(5);

            // data address
            Address = LayoutClass switch
            {
                LayoutClass.Compact => ulong.MaxValue, // invalid address
                LayoutClass.Contiguous => superblock.ReadOffset(reader),
                LayoutClass.Chunked => superblock.ReadOffset(reader),
                _ => throw new NotSupportedException($"The layout class '{LayoutClass}' is not supported.")
            };

            // dimension sizes
            DimensionSizes = new uint[Rank];

            for (int i = 0; i < Rank; i++)
            {
                DimensionSizes[i] = reader.ReadUInt32();
            }

            // dataset element size
            if (LayoutClass == LayoutClass.Chunked)
                DatasetElementSize = reader.ReadUInt32();

            // compact data size
            if (LayoutClass == LayoutClass.Compact)
            {
                var compactDataSize = reader.ReadUInt32();
                CompactData = reader.ReadBytes((int)compactDataSize);
            }
            else
            {
                CompactData = Array.Empty<byte>();
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

        public byte Rank { get; set; }
        public uint[] DimensionSizes { get; set; }
        public uint DatasetElementSize { get; set; }
        public byte[] CompactData { get; set; }

        #endregion
    }
}
