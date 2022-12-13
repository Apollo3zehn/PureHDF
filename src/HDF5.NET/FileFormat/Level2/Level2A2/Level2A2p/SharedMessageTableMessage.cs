namespace HDF5.NET
{
    internal class SharedMessageTableMessage : Message
    {
        #region Fields

        private byte _version;

        #endregion

        #region Constructors

        public SharedMessageTableMessage(H5BinaryReader reader, Superblock superblock) : base(reader)
        {
            // version
            Version = reader.ReadByte();

            // shared object header message table address
            SharedObjectHeaderMessageTableAddress = superblock.ReadOffset(reader);

            // index count
            IndexCount = reader.ReadByte();
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
                    throw new FormatException($"Only version 0 instances of type {nameof(SharedMessageTableMessage)} are supported.");

                _version = value;
            }
        }

        public ulong SharedObjectHeaderMessageTableAddress { get; set; }
        public byte IndexCount { get; set; }

        public SharedObjectHeaderMessageTable SharedObjectHeaderMessageTable
        {
            get
            {
                Reader.Seek((long)SharedObjectHeaderMessageTableAddress, SeekOrigin.Begin);
                return new SharedObjectHeaderMessageTable(Reader);
            }
        }

        #endregion
    }
}
