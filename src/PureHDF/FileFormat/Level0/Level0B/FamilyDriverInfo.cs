namespace PureHDF
{
    internal class FamilyDriverInfo : DriverInfo
    {
        #region Constructors

        public FamilyDriverInfo(H5BaseReader reader)
        {
            MemberFileSize = reader.ReadUInt64();
        }

        #endregion

        #region Properties

        public ulong MemberFileSize { get; set; }

        #endregion
    }
}
