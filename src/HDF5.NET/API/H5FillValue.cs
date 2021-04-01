using System.Linq;

namespace HDF5.NET
{
    public partial class H5FillValue
    {
        #region Properties

        public byte[]? Value => _fillValue.IsDefined 
            ? _fillValue.Value.ToArray()
            : null;

        #endregion
    }
}
