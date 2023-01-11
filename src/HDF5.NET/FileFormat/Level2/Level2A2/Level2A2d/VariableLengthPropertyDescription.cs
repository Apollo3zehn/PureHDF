namespace HDF5.NET
{
    internal class VariableLengthPropertyDescription : DatatypePropertyDescription
    {
        #region Constructors

        public VariableLengthPropertyDescription(H5BinaryReader reader)
        {
            BaseType = new DatatypeMessage(reader);
        }

        #endregion

        #region Properties

        public DatatypeMessage BaseType { get; set; }

        #endregion
    }
}