namespace HDF5.NET
{
    public class DriverInfoBlock
    {
        #region Constructors

        public DriverInfoBlock()
        {
            //
        }

        #endregion

        #region Properties

        public byte Version { get; set; }
        public string DriverId { get; set; }
        public ushort DriverInfoSize { get; set; }
        public byte[] DriverInfo { get; set; }

        #endregion
    }
}
