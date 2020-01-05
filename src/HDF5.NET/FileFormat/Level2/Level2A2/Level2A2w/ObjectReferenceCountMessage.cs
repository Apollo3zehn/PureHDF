namespace HDF5.NET
{
    public class ObjectReferenceCountMessage
    {
        #region Constructors

        public ObjectReferenceCountMessage()
        {
            //
        }

        #endregion

        #region Properties

        public byte Version { get; set; }
        public uint ReferenceCount { get; set; }

        #endregion
    }
}
