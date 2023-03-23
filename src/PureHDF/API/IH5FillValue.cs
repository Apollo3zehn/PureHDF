namespace PureHDF;

/// <summary>
/// An HDF5 fill value.
/// </summary>
public interface IH5FillValue
{
    /// <summary>
    /// Gets the raw fill value as array of <see cref="byte"/>.
    /// </summary>
    byte[]? Value { get; }

    /// <summary>
    /// Gets the fill value as value of type <typeparamref name="T"/>. The type parameter <typeparamref name="T"/> must match the <see langword="unmanaged" /> constraint.
    /// </summary>
    T? GetValue<T>() where T : unmanaged;
}