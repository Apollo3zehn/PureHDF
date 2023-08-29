namespace PureHDF;

/// <summary>
/// An HDF5 attribute.
/// </summary>
public interface IH5Attribute
{
    /// <summary>
    /// Gets the attribute name.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the data space.
    /// </summary>
    IH5Dataspace Space { get; }

    /// <summary>
    /// Gets the data type.
    /// </summary>
    IH5DataType Type { get; }

    /// <summary>
    /// Reads the data.
    /// </summary>
    /// <typeparam name="T">The type of the data to read.</typeparam>
    /// <returns>The read data of type <typeparamref name="T"/>.</returns>
    T Read<T>();
}