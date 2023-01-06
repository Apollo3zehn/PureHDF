using System.Diagnostics;

namespace HDF5.NET
{
    [DebuggerDisplay("{Name}: Class = '{Message.Datatype.Class}'")]
    partial class H5Attribute
    {
        #region Fields

        private H5Dataspace? _space;
        private H5DataType? _type;
        private Superblock _superblock;

        #endregion

        #region Constructors

        internal H5Attribute(AttributeMessage message, Superblock superblock)
        {
            Message = message;
            _superblock = superblock;
        }

        #endregion

        #region Properties

        internal AttributeMessage Message { get; }

        #endregion
    }
}
