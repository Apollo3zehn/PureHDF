namespace HDF5.NET
{
    public abstract class CompoundPropertyDescription : DatatypePropertyDescription
    {
        #region Constructors

        public CompoundPropertyDescription(H5BinaryReader reader) : base(reader)
        {
            //
        }

        #endregion

        #region Properties

        public string Name { get; set; }
        public ulong MemberByteOffset { get; set; }
        public DatatypeMessage MemberTypeMessage { get; set; }

        #endregion
    }
}