using PureHDF.Selections;

namespace PureHDF;

/// <summary>
/// An HDF5 dataset.
/// </summary>
public interface IH5Dataset : IH5AttributableObject
{
    /// <summary>
    /// Gets the data space.
    /// </summary>
    IH5Dataspace Space { get; }

    /// <summary>
    /// Gets the data type.
    /// </summary>
    IH5DataType Type { get; }

    /// <summary>
    /// Gets the data layout.
    /// </summary>
    IH5DataLayout Layout { get; }

    /// <summary>
    /// Gets the fill value.
    /// </summary>
    IH5FillValue FillValue { get; }

    /// <summary>
    /// Reads the data.
    /// </summary>
    /// <typeparam name="T">The type of the data to read.</typeparam>
    /// <param name="fileSelection">The selection within the source HDF5 dataset.</param>
    /// <param name="memorySelection">The selection within the destination memory.</param>
    /// <param name="memoryDims">The dimensions of the destination memory buffer.</param>
    /// <returns>The read data as array of <typeparamref name="T"/>.</returns>
    T Read<T>(Selection? fileSelection = null, Selection? memorySelection = null, ulong[]? memoryDims = null);

    /// <summary>
    /// Reads the data into the provided buffer.
    /// </summary>
    /// <typeparam name="T">The type of the data to read.</typeparam>
    /// <param name="buffer">The buffer to read the data into.</param>
    /// <param name="fileSelection">The selection within the source HDF5 dataset.</param>
    /// <param name="memorySelection">The selection within the destination memory.</param>
    /// <param name="memoryDims">The dimensions of the destination memory buffer.</param>
    void Read<T>(T buffer, Selection? fileSelection = null, Selection? memorySelection = null, ulong[]? memoryDims = null);

    /// <summary>
    /// Reads the data asynchronously. More information: <seealso href="https://github.com/Apollo3zehn/PureHDF#8-asynchronous-data-access-net-6">PureHDF</seealso>.
    /// </summary>
    /// <typeparam name="T">The type of the data to read.</typeparam>
    /// <param name="fileSelection">The selection within the source HDF5 dataset.</param>
    /// <param name="memorySelection">The selection within the target memory.</param>
    /// <param name="memoryDims">The dimensions of the target memory buffer.</param>
    /// <param name="cancellationToken">A token to cancel the current operation.</param>
    /// <returns>A task which returns the read data as array of <typeparamref name="T"/>.</returns>
    Task<T> ReadAsync<T>(Selection? fileSelection = null, Selection? memorySelection = null, ulong[]? memoryDims = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads the data asynchronously into the provided buffer. More information: <seealso href="https://github.com/Apollo3zehn/PureHDF#8-asynchronous-data-access-net-6">PureHDF</seealso>.
    /// </summary>
    /// <typeparam name="T">The type of the data to read.</typeparam>
    /// <param name="buffer">The buffer to read the data into.</param>
    /// <param name="fileSelection">The selection within the source HDF5 dataset.</param>
    /// <param name="memorySelection">The selection within the target memory.</param>
    /// <param name="memoryDims">The dimensions of the target memory buffer.</param>
    /// <param name="cancellationToken">A token to cancel the current operation.</param>
    Task ReadAsync<T>(T buffer, Selection? fileSelection = null, Selection? memorySelection = null, ulong[]? memoryDims = null, CancellationToken cancellationToken = default);
}