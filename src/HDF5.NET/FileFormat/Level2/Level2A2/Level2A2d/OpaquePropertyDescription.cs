using System.IO;

namespace HDF5.NET
{
    public class OpaquePropertyDescription : DatatypePropertyDescription
    {
        #region Constructors

        public OpaquePropertyDescription(BinaryReader reader) : base(reader)
        {
            this.AsciiTag = H5Utils.ReadNullTerminatedString(reader, pad: true);
        }

        #endregion

        #region Properties

        public string AsciiTag { get; set; }

        #endregion
    }
}