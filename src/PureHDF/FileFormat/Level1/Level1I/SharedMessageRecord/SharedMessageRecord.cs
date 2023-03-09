namespace PureHDF
{
    internal abstract class SharedMessageRecord
    {
        #region Constructors

        public SharedMessageRecord(H5DriverBase driver)
        {
            // message location
            MessageLocation = (MessageLocation)driver.ReadByte();
        }

        #endregion

        #region Properties

        public MessageLocation MessageLocation { get; set; }

        #endregion
    }
}
