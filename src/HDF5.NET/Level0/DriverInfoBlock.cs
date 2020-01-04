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
        public byte Reserved1 { get; set; }
        public byte Reserved2 { get; set; }
        public byte Reserved3 { get; set; }
        public uint DriverInfoSize { get; set; }
        public ulong DriverId { get; set; }
        public byte[] DriverInfo { get; set; }

        #endregion
    }
}
