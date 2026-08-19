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
    /// <param name="memoryDims">The dimensions of the resulting buffer.</param>
    /// <returns>The read data of type <typeparamref name="T"/>.</returns>
    T Read<T>(ulong[]? memoryDims = null);

    /// <summary>
    /// Reads the data into the provided buffer.
    /// </summary>
    /// <typeparam name="T">The type of the data to read.</typeparam>
    /// <param name="buffer">The buffer to read the data into.</param>
    /// <param name="memoryDims">The dimensions of the resulting buffer.</param>
    void Read<T>(T buffer, ulong[]? memoryDims = null);

    /// <summary>
    /// Reads the data asynchronously.
    /// </summary>
    /// <typeparam name="T">The type of the data to read.</typeparam>
    /// <param name="memoryDims">The dimensions of the resulting buffer.</param>
    /// <param name="cancellationToken">A token to cancel the current operation.</param>
    /// <returns>A task which returns the read data of type <typeparamref name="T"/>.</returns>
    /// <remarks>
    /// Whether this actually suspends depends on the DATATYPE, not on the size of the attribute. An
    /// attribute's own bytes are decoded into the object header when the object is resolved, so a
    /// fixed-size attribute is already in memory and this completes synchronously. Variable-length and
    /// reference data store only a global heap ID inline, and resolving one reads the file - so a string
    /// attribute genuinely suspends, and on a host that cannot block (a single-threaded WASM runtime)
    /// the synchronous <see cref="Read{T}(ulong[])"/> cannot serve one at all.
    /// </remarks>
    Task<T> ReadAsync<T>(ulong[]? memoryDims = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads the data asynchronously into the provided buffer.
    /// </summary>
    /// <typeparam name="T">The type of the data to read.</typeparam>
    /// <param name="buffer">The buffer to read the data into.</param>
    /// <param name="memoryDims">The dimensions of the resulting buffer.</param>
    /// <param name="cancellationToken">A token to cancel the current operation.</param>
    /// <remarks>See the remarks on <see cref="ReadAsync{T}(ulong[], CancellationToken)"/>.</remarks>
    Task ReadAsync<T>(T buffer, ulong[]? memoryDims = null, CancellationToken cancellationToken = default);
}