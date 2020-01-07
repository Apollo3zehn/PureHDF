using System.IO;

namespace HDF5.NET
{
    public class OpaqueBitFieldDescription : DatatypeBitFieldDescription
    {
        #region Constructors

        public OpaqueBitFieldDescription(BinaryReader reader) : base(reader)
        {
            //
        }

        #endregion

        #region Properties

        public byte AsciiTagByteLength
        {
            get { return this.Data[0]; }
            set { this.Data[0] = value; }
        }

        #endregion
    }
}
