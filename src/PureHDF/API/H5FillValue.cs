using System.Runtime.InteropServices;

namespace PureHDF
{
    /// <summary>
    /// An HDF5 fill value.
    /// </summary>
    public partial class H5FillValue
    {
        #region Properties

        /// <summary>
        /// Gets the raw fill value as array of <see cref="byte"/>.
        /// </summary>
        public byte[]? Value => _fillValue.Value?.ToArray();

        /// <summary>
        /// Gets the fill value as value of type <typeparamref name="T"/>. The type parameter <typeparamref name="T"/> must match the <see langword="unmanaged" /> constraint.
        /// </summary>
        public T? GetValue<T>() where T : unmanaged => _fillValue.Value is null
            ? default
            : MemoryMarshal.Cast<byte, T>(_fillValue.Value.AsSpan())[0];

        #endregion
    }
}
