namespace HDF5.NET
{
    public class ObjectModificationMessage
    {
        #region Constructors

        public ObjectModificationMessage()
        {
            //
        }

        #endregion

        #region Properties

        public byte Version { get; set; }
        public uint SecondsAfterUnixEpoch { get; set; }

        #endregion
    }
}
