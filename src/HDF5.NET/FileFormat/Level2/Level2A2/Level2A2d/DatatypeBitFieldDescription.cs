using System.IO;

namespace HDF5.NET
{
    public abstract class DatatypeBitFieldDescription : FileBlock
    {
        #region Constructors

        public DatatypeBitFieldDescription(BinaryReader reader) : base(reader)
        {
            this.Data = reader.ReadBytes(3);
        }

        #endregion

        #region Properties

        protected byte[] Data { get; }

        #endregion
    }
}
