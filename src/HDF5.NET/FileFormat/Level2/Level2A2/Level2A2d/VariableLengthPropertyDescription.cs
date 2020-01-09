using System.IO;

namespace HDF5.NET
{
    public class VariableLengthPropertyDescription : DatatypePropertyDescription
    {
        #region Constructors

        public VariableLengthPropertyDescription(BinaryReader reader) : base(reader)
        {
            this.BaseType = new DatatypeMessage(reader);
        }

        #endregion

        #region Properties

        public DatatypeMessage BaseType { get; set; }

        #endregion
    }
}