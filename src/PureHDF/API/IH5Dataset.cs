using System.Reflection;

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
    /// <param name="fileSelection">The selection within the source HDF5 dataset.</param>
    /// <param name="memorySelection">The selection within the destination memory.</param>
    /// <param name="memoryDims">The dimensions of the destination memory buffer.</param>
    /// <returns>The read data as array of <see cref="byte"/>.</returns>
    byte[] Read(Selection? fileSelection = null, Selection? memorySelection = null, ulong[]? memoryDims = null);

    /// <summary>
    /// Reads the data. The type parameter <typeparamref name="T"/> must match the <see langword="unmanaged" /> constraint.
    /// </summary>
    /// <typeparam name="T">The type of the data to read.</typeparam>
    /// <param name="fileSelection">The selection within the source HDF5 dataset.</param>
    /// <param name="memorySelection">The selection within the destination memory.</param>
    /// <param name="memoryDims">The dimensions of the destination memory buffer.</param>
    /// <returns>The read data as array of <typeparamref name="T"/>.</returns>
    T[] Read<T>(Selection? fileSelection = null, Selection? memorySelection = null, ulong[]? memoryDims = null) where T : unmanaged;

    /// <summary>
    /// Reads the data. The type parameter <typeparamref name="T"/> must match the <see langword="unmanaged" /> constraint.
    /// </summary>
    /// <typeparam name="T">The type of the data to read.</typeparam>
    /// <param name="buffer">The destination memory buffer.</param>
    /// <param name="fileSelection">The selection within the source HDF5 dataset.</param>
    /// <param name="memorySelection">The selection within the destination memory.</param>
    /// <param name="memoryDims">The dimensions of the destination memory buffer.</param>
    void Read<T>(Memory<T> buffer, Selection? fileSelection = null, Selection? memorySelection = null, ulong[]? memoryDims = null) where T : unmanaged;

    /// <summary>
    /// Reads the compound data. The type parameter <typeparamref name="T"/> must match the <see langword="struct" /> constraint. Nested fields with nullable references are not supported.
    /// </summary>
    /// <typeparam name="T">The type of the data to read.</typeparam>
    /// <param name="getName">An optional function to map the field names of <typeparamref name="T"/> to the member names of the HDF5 compound type.</param>
    /// <param name="fileSelection">The selection within the source HDF5 dataset.</param>
    /// <param name="memorySelection">The selection within the destination memory.</param>
    /// <param name="memoryDims">The dimensions of the destination memory buffer.</param>
    /// <returns>The read data as array of <typeparamref name="T"/>.</returns>
    T[] ReadCompound<T>(Func<FieldInfo, string?>? getName = null, Selection? fileSelection = null, Selection? memorySelection = null, ulong[]? memoryDims = null) where T : struct;

    /// <summary>
    /// Reads the compound data. This is the slowest but most flexible option to read compound data as no prior type knowledge is required.
    /// </summary>
    /// <param name="fileSelection">The selection within the source HDF5 dataset.</param>
    /// <param name="memorySelection">The selection within the target memory.</param>
    /// <param name="memoryDims">The dimensions of the target memory buffer.</param>
    /// <returns>The read data as array of a dictionary with the keys corresponding to the compound member names and the values being the member data.</returns>
    Dictionary<string, object?>[] ReadCompound(Selection? fileSelection = null, Selection? memorySelection = null, ulong[]? memoryDims = null);

    // TODO: Unify names: result, destination, destination

    /// <summary>
    /// Reads the string data.
    /// </summary>
    /// <param name="fileSelection">The selection within the source HDF5 dataset.</param>
    /// <param name="memorySelection">The selection within the destination memory.</param>
    /// <param name="memoryDims">The dimensions of the destination memory buffer.</param>
    /// <returns>The read data as array of <see cref="string"/>.</returns>
    string?[] ReadString(Selection? fileSelection = null, Selection? memorySelection = null, ulong[]? memoryDims = null);

    /// <summary>
    /// Reads the variable-length sequence data.
    /// </summary>
    /// <param name="fileSelection">The selection within the source HDF5 dataset.</param>
    /// <param name="memorySelection">The selection within the destination memory.</param>
    /// <param name="memoryDims">The dimensions of the destination memory buffer.</param>
    /// <returns>The read data as jagged array of <typeparamref name="T"/>.</returns>
    T[]?[] ReadVariableLength<T>(Selection? fileSelection = null, Selection? memorySelection = null, ulong[]? memoryDims = null) where T : struct;

    /// <summary>
    /// Reads the data asynchronously. More information: <seealso href="https://github.com/Apollo3zehn/PureHDF#8-asynchronous-data-access-net-6">PureHDF</seealso>.
    /// </summary>
    /// <param name="fileSelection">The selection within the source HDF5 dataset.</param>
    /// <param name="memorySelection">The selection within the destination memory.</param>
    /// <param name="memoryDims">The dimensions of the destination memory buffer.</param>
    /// <param name="cancellationToken">A token to cancel the current operation.</param>
    /// <returns>A task which returns the read data as array of <see cref="byte"/>.</returns>
    Task<byte[]> ReadAsync(Selection? fileSelection = null, Selection? memorySelection = null, ulong[]? memoryDims = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads the data asynchronously. More information: <seealso href="https://github.com/Apollo3zehn/PureHDF#8-asynchronous-data-access-net-6">PureHDF</seealso>. The type parameter <typeparamref name="T"/> must match the <see langword="unmanaged" /> constraint.
    /// </summary>
    /// <typeparam name="T">The type of the data to read.</typeparam>
    /// <param name="fileSelection">The selection within the source HDF5 dataset.</param>
    /// <param name="memorySelection">The selection within the target memory.</param>
    /// <param name="memoryDims">The dimensions of the target memory buffer.</param>
    /// <param name="cancellationToken">A token to cancel the current operation.</param>
    /// <returns>A task which returns the read data as array of <typeparamref name="T"/>.</returns>
    Task<T[]> ReadAsync<T>(Selection? fileSelection = null, Selection? memorySelection = null, ulong[]? memoryDims = null, CancellationToken cancellationToken = default) where T : unmanaged;

    /// <summary>
    /// Reads the data asynchronously. More information: <seealso href="https://github.com/Apollo3zehn/PureHDF#8-asynchronous-data-access-net-6">PureHDF</seealso>. The type parameter <typeparamref name="T"/> must match the <see langword="unmanaged" /> constraint.
    /// </summary>
    /// <typeparam name="T">The type of the data to read.</typeparam>
    /// <param name="buffer">The destination memory buffer.</param>
    /// <param name="fileSelection">The selection within the source HDF5 dataset.</param>
    /// <param name="memorySelection">The selection within the destination memory.</param>
    /// <param name="memoryDims">The dimensions of the destination memory buffer.</param>
    /// <param name="cancellationToken">A token to cancel the current operation.</param>
    Task ReadAsync<T>(Memory<T> buffer, Selection? fileSelection = null, Selection? memorySelection = null, ulong[]? memoryDims = null, CancellationToken cancellationToken = default) where T : unmanaged;

    /// <summary>
    /// Reads the compound data asynchronously. More information: <seealso href="https://github.com/Apollo3zehn/PureHDF#8-asynchronous-data-access-net-6">PureHDF</seealso>. The type parameter <typeparamref name="T"/> must match the <see langword="struct" /> constraint. Nested fields with nullable references are not supported.
    /// </summary>
    /// <typeparam name="T">The type of the data to read.</typeparam>
    /// <param name="getName">An optional function to map the field names of <typeparamref name="T"/> to the member names of the HDF5 compound type.</param>
    /// <param name="fileSelection">The selection within the source HDF5 dataset.</param>
    /// <param name="memorySelection">The selection within the destination memory.</param>
    /// <param name="memoryDims">The dimensions of the destination memory buffer.</param>
    /// <param name="cancellationToken">A token to cancel the current operation.</param>
    /// <returns>A task which returns the read data as array of <typeparamref name="T"/>.</returns>
    Task<T[]> ReadCompoundAsync<T>(Func<FieldInfo, string?>? getName = null, Selection? fileSelection = null, Selection? memorySelection = null, ulong[]? memoryDims = null, CancellationToken cancellationToken = default) where T : struct;

    /// <summary>
    /// Reads the compound data asynchronously. More information: <seealso href="https://github.com/Apollo3zehn/PureHDF#8-asynchronous-data-access-net-6">PureHDF</seealso>. This is the slowest but most flexible option to read compound data as no prior type knowledge is required.
    /// </summary>
    /// <param name="fileSelection">The selection within the source HDF5 dataset.</param>
    /// <param name="memorySelection">The selection within the destination memory.</param>
    /// <param name="memoryDims">The dimensions of the destination memory buffer.</param>
    /// <param name="cancellationToken">A token to cancel the current operation.</param>
    /// <returns>A task which returns the read data as array of a dictionary with the keys corresponding to the compound member names and the values being the member data.</returns>
    Task<Dictionary<string, object?>[]> ReadCompoundAsync(Selection? fileSelection = null, Selection? memorySelection = null, ulong[]? memoryDims = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads the string data asynchronously. More information: <seealso href="https://github.com/Apollo3zehn/PureHDF#8-asynchronous-data-access-net-6">PureHDF</seealso>.
    /// </summary>
    /// <param name="fileSelection">The selection within the source HDF5 dataset.</param>
    /// <param name="memorySelection">The selection within the destination memory.</param>
    /// <param name="memoryDims">The dimensions of the destination memory buffer.</param>
    /// <param name="cancellationToken">A token to cancel the current operation.</param>
    /// <returns>A task which returns the read data as array of <see cref="string"/>.</returns>
    Task<string?[]> ReadStringAsync(Selection? fileSelection = null, Selection? memorySelection = null, ulong[]? memoryDims = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads the variable-length sequence data asynchronously. More information: <seealso href="https://github.com/Apollo3zehn/PureHDF#8-asynchronous-data-access-net-6">PureHDF</seealso>.
    /// </summary>
    /// <param name="fileSelection">The selection within the source HDF5 dataset.</param>
    /// <param name="memorySelection">The selection within the destination memory.</param>
    /// <param name="memoryDims">The dimensions of the destination memory buffer.</param>
    /// <param name="cancellationToken">A token to cancel the current operation.</param>
    /// <returns>A task which returns the read data as jagged array of <typeparamref name="T"/>.</returns>
    Task<T[]?[]> ReadVariableLengthAsync<T>(Selection? fileSelection = null, Selection? memorySelection = null, ulong[]? memoryDims = null, CancellationToken cancellationToken = default) where T : struct;
}