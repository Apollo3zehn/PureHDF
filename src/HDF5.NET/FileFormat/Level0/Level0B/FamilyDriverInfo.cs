namespace HDF5.NET
{
    internal class FamilyDriverInfo : DriverInfo
    {
        #region Constructors

        public FamilyDriverInfo(H5BinaryReader reader)
        {
            MemberFileSize = reader.ReadUInt64();
        }

        #endregion

        #region Properties

        public ulong MemberFileSize { get; set; }

        #endregion
    }
}
