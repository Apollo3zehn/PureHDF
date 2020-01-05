namespace HDF5.NET
{
    public class DriverInfoMessage
    {
        #region Constructors

        public DriverInfoMessage()
        {
            //
        }

        #endregion

        #region Properties

        public byte Version { get; set; }
        public string DriverId { get; set; }
        public ushort GroupInternalNodeK { get; set; }
        public ushort GroupLeafNodeK { get; set; }

        #endregion
    }
}
