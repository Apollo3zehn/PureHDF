using System.Diagnostics;

namespace HDF5.NET
{
    [DebuggerDisplay("{Name}")]
    public abstract class H5Link
    {
        #region Constructors

        internal H5Link(string name, ObjectHeader objectHeader)
        {
            this.Name = name;
            this.ObjectHeader = objectHeader;
        }

        #endregion

        #region Properties

        public string Name { get; }

        public ObjectHeader ObjectHeader { get; }

        #endregion
    }
}
