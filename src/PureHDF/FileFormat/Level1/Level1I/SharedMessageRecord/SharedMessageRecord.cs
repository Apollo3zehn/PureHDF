namespace PureHDF
{
    internal abstract class SharedMessageRecord
    {
        #region Constructors

        public SharedMessageRecord(H5BaseReader reader)
        {
            // message location
            MessageLocation = (MessageLocation)reader.ReadByte();
        }

        #endregion

        #region Properties

        public MessageLocation MessageLocation { get; set; }

        #endregion
    }
}
