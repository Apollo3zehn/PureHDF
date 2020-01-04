namespace HDF5.NET
{
    public class MultiDriverInfo : DriverInfo
    {
        #region Constructors

        public MultiDriverInfo()
        {
            //
        }

        #endregion

        #region Properties

        public MemberMapping MemberMapping1 { get; set; }
        public MemberMapping MemberMapping2 { get; set; }
        public MemberMapping MemberMapping3 { get; set; }
        public MemberMapping MemberMapping4 { get; set; }
        public MemberMapping MemberMapping5 { get; set; }
        public MemberMapping MemberMapping6 { get; set; }
        public byte Reserved1 { get; set; }
        public byte Reserved2 { get; set; }
        public byte[] RemainingData { get; set; }

        #endregion
    }
}
