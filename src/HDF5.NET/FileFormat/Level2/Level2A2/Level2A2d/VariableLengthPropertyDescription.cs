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

        public DataypeMessage BaseType { get; set; }

        #endregion
    }
}