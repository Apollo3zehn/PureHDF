namespace HDF5.NET
{
    public class OldFillValueMessage
    {
        #region Constructors

        public OldFillValueMessage()
        {
            //
        }

        #endregion

        #region Properties

        public uint Size { get; set; }
        public byte[] FillValue { get; set; }

        #endregion
    }
}
