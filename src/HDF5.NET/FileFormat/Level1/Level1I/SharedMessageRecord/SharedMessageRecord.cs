namespace HDF5.NET
{
    internal abstract class SharedMessageRecord : FileBlock
    {
        #region Constructors

        public SharedMessageRecord(H5BinaryReader reader) : base(reader)
        {
            // message location
            this.MessageLocation = (MessageLocation)reader.ReadByte();
        }

        #endregion

        #region Properties

        public MessageLocation MessageLocation { get; set; }

        #endregion
    }
}
