using System.Diagnostics;

namespace HDF5.NET
{
    [DebuggerDisplay("{Name}: Class = '{Datatype.Class}'")]
    public class H5CommitedDatatype : H5Link
    {
        #region Constructors

        internal H5CommitedDatatype(string name, ObjectHeader objectHeader) 
            : base(name)
        {
            this.Datatype = objectHeader.GetMessage<DatatypeMessage>();
        }

        #endregion

        #region Properties

        public DatatypeMessage Datatype { get; }

        #endregion
    }
}