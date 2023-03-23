using System.Reflection;

namespace PureHDF;

/// <summary>
/// A native HDF5 dataset.
/// </summary>
public interface INativeDataset : IH5Dataset
{
    /// <summary>
    /// Queries the data. More information: <seealso href="https://github.com/Apollo3zehn/PureHDF#43-experimental-iqueryable-1-dimensional-data-only">PureHDF</seealso>.
    /// </summary>
    /// <typeparam name="T">The type of the data to read.</typeparam>
    /// <param name="datasetAccess">The dataset access properties.</param>
    /// <param name="memorySelection">The selection within the destination memory.</param>
    /// <param name="memoryDims">The dimensions of the destination memory buffer.</param>
    /// <returns>A queryable of type <typeparamref name="T"/>.</returns>
    IQueryable<T> AsQueryable<T>(H5DatasetAccess datasetAccess, Selection? memorySelection = null, ulong[]? memoryDims = null) where T : unmanaged;

    /// <summary>
    /// Reads the data.
    /// </summary>
    /// <param name="fileSelection">The selection within the source HDF5 dataset.</param>
    /// <param name="memorySelection">The selection within the destination memory.</param>
    /// <param name="memoryDims">The dimensions of the destination memory buffer.</param>
    /// <param name="datasetAccess">The dataset access properties.</param>
    /// <returns>The read data as array of <see cref="byte"/>.</returns>
    byte[] Read(H5DatasetAccess datasetAccess, Selection? fileSelection = null, Selection? memorySelection = null, ulong[]? memoryDims = null);

    /// <summary>
    /// Reads the data. The type parameter <typeparamref name="T"/> must match the <see langword="unmanaged" /> constraint.
    /// </summary>
    /// <typeparam name="T">The type of the data to read.</typeparam>
    /// <param name="datasetAccess">The dataset access properties.</param>
    /// <param name="fileSelection">The selection within the source HDF5 dataset.</param>
    /// <param name="memorySelection">The selection within the destination memory.</param>
    /// <param name="memoryDims">The dimensions of the destination memory buffer.</param>
    /// <returns>The read data as array of <typeparamref name="T"/>.</returns>
    T[] Read<T>(H5DatasetAccess datasetAccess, Selection? fileSelection = null, Selection? memorySelection = null, ulong[]? memoryDims = null) where T : unmanaged;

    /// <summary>
    /// Reads the data. The type parameter <typeparamref name="T"/> must match the <see langword="unmanaged" /> constraint.
    /// </summary>
    /// <typeparam name="T">The type of the data to read.</typeparam>
    /// <param name="buffer">The destination memory buffer.</param>
    /// <param name="datasetAccess">The dataset access properties.</param>
    /// <param name="fileSelection">The selection within the source HDF5 dataset.</param>
    /// <param name="memorySelection">The selection within the destination memory.</param>
    /// <param name="memoryDims">The dimensions of the destination memory buffer.</param>
    void Read<T>(Memory<T> buffer, H5DatasetAccess datasetAccess, Selection? fileSelection = null, Selection? memorySelection = null, ulong[]? memoryDims = null) where T : unmanaged;

    /// <summary>
    /// Reads the compound data. The type parameter <typeparamref name="T"/> must match the <see langword="struct" /> constraint. Nested fields with nullable references are not supported.
    /// </summary>
    /// <typeparam name="T">The type of the data to read.</typeparam>
    /// <param name="datasetAccess">The dataset access properties.</param>
    /// <param name="getName">An optional function to map the field names of <typeparamref name="T"/> to the member names of the HDF5 compound type.</param>
    /// <param name="fileSelection">The selection within the source HDF5 dataset.</param>
    /// <param name="memorySelection">The selection within the destination memory.</param>
    /// <param name="memoryDims">The dimensions of the destination memory buffer.</param>
    /// <returns>The read data as array of <typeparamref name="T"/>.</returns>
    T[] ReadCompound<T>(H5DatasetAccess datasetAccess, Func<FieldInfo, string>? getName = null, Selection? fileSelection = null, Selection? memorySelection = null, ulong[]? memoryDims = null) where T : struct;

    /// <summary>
    /// Reads the compound data. This is the slowest but most flexible option to read compound data as no prior type knowledge is required.
    /// </summary>
    /// <param name="datasetAccess">The dataset access properties.</param>
    /// <param name="fileSelection">The selection within the source HDF5 dataset.</param>
    /// <param name="memorySelection">The selection within the target memory.</param>
    /// <param name="memoryDims">The dimensions of the target memory buffer.</param>
    /// <returns>The read data as array of a dictionary with the keys corresponding to the compound member names and the values being the member data.</returns>
    Dictionary<string, object?>[] ReadCompound(H5DatasetAccess datasetAccess, Selection? fileSelection = null, Selection? memorySelection = null, ulong[]? memoryDims = null);

    // TODO: Unify names: result, destination, destination

    /// <summary>
    /// Reads the string data.
    /// </summary>
    /// <param name="datasetAccess">The dataset access properties.</param>
    /// <param name="fileSelection">The selection within the source HDF5 dataset.</param>
    /// <param name="memorySelection">The selection within the destination memory.</param>
    /// <param name="memoryDims">The dimensions of the destination memory buffer.</param>
    /// <returns>The read data as array of <see cref="string"/>.</returns>
    string?[] ReadString(H5DatasetAccess datasetAccess, Selection? fileSelection = null, Selection? memorySelection = null, ulong[]? memoryDims = null);

    /// <summary>
    /// Reads the data asynchronously. More information: <seealso href="https://github.com/Apollo3zehn/PureHDF#8-asynchronous-data-access-net-6">PureHDF</seealso>.
    /// </summary>
    /// <param name="fileSelection">The selection within the source HDF5 dataset.</param>
    /// <param name="memorySelection">The selection within the destination memory.</param>
    /// <param name="memoryDims">The dimensions of the destination memory buffer.</param>
    /// <param name="datasetAccess">The dataset access properties.</param>
    /// <param name="cancellationToken">A token to cancel the current operation.</param>
    /// <returns>A task which returns the read data as array of <see cref="byte"/>.</returns>
    Task<byte[]> ReadAsync(H5DatasetAccess datasetAccess, Selection? fileSelection = null, Selection? memorySelection = null, ulong[]? memoryDims = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads the data asynchronously. More information: <seealso href="https://github.com/Apollo3zehn/PureHDF#8-asynchronous-data-access-net-6">PureHDF</seealso>. The type parameter <typeparamref name="T"/> must match the <see langword="unmanaged" /> constraint.
    /// </summary>
    /// <typeparam name="T">The type of the data to read.</typeparam>
    /// <param name="datasetAccess">The dataset access properties.</param>
    /// <param name="fileSelection">The selection within the source HDF5 dataset.</param>
    /// <param name="memorySelection">The selection within the target memory.</param>
    /// <param name="memoryDims">The dimensions of the target memory buffer.</param>
    /// <param name="cancellationToken">A token to cancel the current operation.</param>
    /// <returns>A task which returns the read data as array of <typeparamref name="T"/>.</returns>
    Task<T[]> ReadAsync<T>(H5DatasetAccess datasetAccess, Selection? fileSelection = null, Selection? memorySelection = null, ulong[]? memoryDims = null, CancellationToken cancellationToken = default) where T : unmanaged;

    /// <summary>
    /// Reads the data asynchronously. More information: <seealso href="https://github.com/Apollo3zehn/PureHDF#8-asynchronous-data-access-net-6">PureHDF</seealso>. The type parameter <typeparamref name="T"/> must match the <see langword="unmanaged" /> constraint.
    /// </summary>
    /// <typeparam name="T">The type of the data to read.</typeparam>
    /// <param name="buffer">The destination memory buffer.</param>
    /// <param name="datasetAccess">The dataset access properties.</param>
    /// <param name="fileSelection">The selection within the source HDF5 dataset.</param>
    /// <param name="memorySelection">The selection within the destination memory.</param>
    /// <param name="memoryDims">The dimensions of the destination memory buffer.</param>
    /// <param name="cancellationToken">A token to cancel the current operation.</param>
    Task ReadAsync<T>(Memory<T> buffer, H5DatasetAccess datasetAccess, Selection? fileSelection = null, Selection? memorySelection = null, ulong[]? memoryDims = null, CancellationToken cancellationToken = default) where T : unmanaged;

    /// <summary>
    /// Reads the compound data asynchronously. More information: <seealso href="https://github.com/Apollo3zehn/PureHDF#8-asynchronous-data-access-net-6">PureHDF</seealso>. The type parameter <typeparamref name="T"/> must match the <see langword="struct" /> constraint. Nested fields with nullable references are not supported.
    /// </summary>
    /// <typeparam name="T">The type of the data to read.</typeparam>
    /// <param name="datasetAccess">The dataset access properties.</param>
    /// <param name="getName">An optional function to map the field names of <typeparamref name="T"/> to the member names of the HDF5 compound type.</param>
    /// <param name="fileSelection">The selection within the source HDF5 dataset.</param>
    /// <param name="memorySelection">The selection within the destination memory.</param>
    /// <param name="memoryDims">The dimensions of the destination memory buffer.</param>
    /// <param name="cancellationToken">A token to cancel the current operation.</param>
    /// <returns>A task which returns the read data as array of <typeparamref name="T"/>.</returns>
    Task<T[]> ReadCompoundAsync<T>(H5DatasetAccess datasetAccess, Func<FieldInfo, string>? getName = null, Selection? fileSelection = null, Selection? memorySelection = null, ulong[]? memoryDims = null, CancellationToken cancellationToken = default) where T : struct;

    /// <summary>
    /// Reads the compound data asynchronously. More information: <seealso href="https://github.com/Apollo3zehn/PureHDF#8-asynchronous-data-access-net-6">PureHDF</seealso>. This is the slowest but most flexible option to read compound data as no prior type knowledge is required.
    /// </summary>
    /// <param name="datasetAccess">The dataset access properties.</param>
    /// <param name="fileSelection">The selection within the source HDF5 dataset.</param>
    /// <param name="memorySelection">The selection within the destination memory.</param>
    /// <param name="memoryDims">The dimensions of the destination memory buffer.</param>
    /// <param name="cancellationToken">A token to cancel the current operation.</param>
    /// <returns>A task which returns the read data as array of a dictionary with the keys corresponding to the compound member names and the values being the member data.</returns>
    Task<Dictionary<string, object?>[]> ReadCompoundAsync(H5DatasetAccess datasetAccess, Selection? fileSelection = null, Selection? memorySelection = null, ulong[]? memoryDims = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads the string data asynchronously. More information: <seealso href="https://github.com/Apollo3zehn/PureHDF#8-asynchronous-data-access-net-6">PureHDF</seealso>.
    /// </summary>
    /// <param name="datasetAccess">The dataset access properties.</param>
    /// <param name="fileSelection">The selection within the source HDF5 dataset.</param>
    /// <param name="memorySelection">The selection within the destination memory.</param>
    /// <param name="memoryDims">The dimensions of the destination memory buffer.</param>
    /// <param name="cancellationToken">A token to cancel the current operation.</param>
    /// <returns>A task which returns the read data as array of <see cref="string"/>.</returns>
    Task<string?[]> ReadStringAsync(H5DatasetAccess datasetAccess, Selection? fileSelection = null, Selection? memorySelection = null, ulong[]? memoryDims = null, CancellationToken cancellationToken = default);
}