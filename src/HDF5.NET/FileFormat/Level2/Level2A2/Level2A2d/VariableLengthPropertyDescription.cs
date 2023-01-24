namespace HDF5.NET
{
    internal class VariableLengthPropertyDescription : DatatypePropertyDescription
    {
        #region Constructors

        public VariableLengthPropertyDescription(H5BaseReader reader)
        {
            BaseType = new DatatypeMessage(reader);
        }

        #endregion

        #region Properties

        public DatatypeMessage BaseType { get; set; }

        #endregion
    }
}