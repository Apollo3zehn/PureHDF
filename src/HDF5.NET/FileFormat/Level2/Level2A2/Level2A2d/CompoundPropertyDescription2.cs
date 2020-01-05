namespace HDF5.NET
{
    public class CompoundPropertyDescription2 : DatatypePropertyDescription
    {
        #region Constructors

        public CompoundPropertyDescription2()
        {
            //
        }

        #endregion

        #region Properties

        public string Name { get; set; }
        public uint MemberByteOffset { get; set; }
        public DataypeMessage MemberTypeMessage { get; set; }

        #endregion
    }
}