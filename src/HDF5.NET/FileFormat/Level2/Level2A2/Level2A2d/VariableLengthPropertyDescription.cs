namespace HDF5.NET
{
    public class VariableLengthPropertyDescription : DatatypePropertyDescription
    {
        #region Constructors

        public VariableLengthPropertyDescription()
        {
            //
        }

        #endregion

        #region Properties

        public DatatypeMessage BaseType { get; set; }

        #endregion
    }
}