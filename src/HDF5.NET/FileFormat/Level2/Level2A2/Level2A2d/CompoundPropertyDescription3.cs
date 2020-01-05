namespace HDF5.NET
{
    public class CompoundPropertyDescription3 : DatatypePropertyDescription
    {
        #region Constructors

        public CompoundPropertyDescription3()
        {
            //
        }

        #endregion

        #region Properties

        public string Name { get; set; }
        public uint MemberByteOffset { get; set; }
        public DatatypeMessage MemberTypeMessage { get; set; }

        #endregion
    }
}