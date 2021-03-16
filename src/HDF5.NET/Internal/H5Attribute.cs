using System.Diagnostics;

namespace HDF5.NET
{
    [DebuggerDisplay("{Name}: Class = '{Message.Datatype.Class}'")]
    public partial class H5Attribute
    {
        #region Fields

        private Superblock _superblock;

        #endregion

        #region Constructors

        internal H5Attribute(AttributeMessage message, Superblock superblock)
        {
            this.Message = message;
            _superblock = superblock;
        }

        #endregion
    }
}
