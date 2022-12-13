namespace HDF5.NET
{
    internal class BTreeKValuesMessage : Message
    {
        #region Fields

        private byte _version;

        #endregion

        #region Constructors

        public BTreeKValuesMessage(H5BinaryReader reader) : base(reader)
        {
            // version
            Version = reader.ReadByte();

            // indexed stroage internal node k
            IndexedStorageInternalNodeK = reader.ReadUInt16();

            // group internal node k
            GroupInternalNodeK = reader.ReadUInt16();

            // group leaf node k
            GroupLeafNodeK = reader.ReadUInt16();
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
                if (value != 0)
                    throw new FormatException($"Only version 0 instances of type {nameof(BTreeKValuesMessage)} are supported.");

                _version = value;
            }
        }

        public ushort IndexedStorageInternalNodeK { get; set; }
        public ushort GroupInternalNodeK { get; set; }
        public ushort GroupLeafNodeK { get; set; }

        #endregion
    }
}
