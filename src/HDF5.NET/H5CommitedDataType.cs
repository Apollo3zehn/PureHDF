using System.Diagnostics;

namespace HDF5.NET
{
    [DebuggerDisplay("{Name}: Class = '{DataType.Class}'")]
    public class H5CommitedDataType : H5Link
    {
        #region Constructors

        internal H5CommitedDataType(string name, ObjectHeader objectHeader) 
            : base(name)
        {
            this.DataType = objectHeader.GetMessage<DatatypeMessage>();
        }

        #endregion

        #region Properties

        public DatatypeMessage DataType { get; }

        #endregion
    }
}
