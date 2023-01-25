using System.Diagnostics;

namespace PureHDF
{
    [DebuggerDisplay("{Name}: Class = '{Message.Datatype.Class}'")]
    partial class H5Attribute
    {
        #region Fields

        private H5Dataspace? _space;
        private H5DataType? _type;
        private H5Context _context;

        #endregion

        #region Constructors

        internal H5Attribute(H5Context context, AttributeMessage message)
        {
            _context = context;
            Message = message;
        }

        #endregion

        #region Properties

        internal AttributeMessage Message { get; }

        #endregion
    }
}
