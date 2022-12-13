namespace HDF5.NET
{
    internal class BogusMessage : Message
    {
        #region Fields

        private uint _bogusValue;

        #endregion

        #region Constructors

        public BogusMessage(H5BinaryReader reader) : base(reader)
        {
            BogusValue = reader.ReadUInt32();
        }

        #endregion

        #region Properties

        public uint BogusValue
        {
            get
            {
                return _bogusValue;
            }
            set
            {
                if (value.ToString("X") != "deadbeef")
                    throw new FormatException($"The bogus value of the {nameof(BogusMessage)} instance is invalid.");

                _bogusValue = value;
            }
        }

        #endregion
    }
}
