namespace HDF5.NET
{
    public class FamilyDriverInfo : DriverInfo
    {
        #region Constructors

        public FamilyDriverInfo(H5BinaryReader reader) : base(reader)
        {
            this.MemberFileSize = reader.ReadUInt64();
        }

        #endregion

        #region Properties

        public ulong MemberFileSize { get; set; }

        #endregion
    }
}
