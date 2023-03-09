namespace PureHDF
{
    internal class ObjectReferenceCountMessage : Message
    {
        #region Fields

        private byte _version;

        #endregion

        #region Constructors

        public ObjectReferenceCountMessage(H5DriverBase driver)
        {
            // version
            Version = driver.ReadByte();

            // reference count
            ReferenceCount = driver.ReadUInt32();
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
                    throw new FormatException($"Only version 0 instances of type {nameof(ObjectReferenceCountMessage)} are supported.");

                _version = value;
            }
        }

        public uint ReferenceCount { get; set; }

        #endregion
    }
}
