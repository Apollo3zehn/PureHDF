namespace HDF5.NET
{
    internal class SharedMessageTableMessage : Message
    {
        #region Fields

        private byte _version;
        private H5BinaryReader _reader;

        #endregion

        #region Constructors

        public SharedMessageTableMessage(H5Context context)
        {
            var (reader, superblock) = context;
            _reader = context.Reader;

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
                _reader.Seek((long)SharedObjectHeaderMessageTableAddress, SeekOrigin.Begin);
                return new SharedObjectHeaderMessageTable(_reader);
            }
        }

        #endregion
    }
}
