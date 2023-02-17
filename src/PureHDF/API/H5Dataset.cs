using System.Reflection;

namespace PureHDF
{
    /// <summary>
    /// An HDF5 dataset.
    /// </summary>
    public partial class H5Dataset : H5AttributableObject
    {
        #region Properties

        /// <summary>
        /// A reference to the <see cref="H5File"/> that this dataset belongs to.
        /// </summary>
        public H5File File { get; }

        /// <summary>
        /// Gets the data space.
        /// </summary>
        public H5Dataspace Space
        {
            get
            {
                _space ??= new H5Dataspace(InternalDataspace);

                return _space;
            }
        }

        /// <summary>
        /// Gets the data type.
        /// </summary>
        public H5DataType Type
        {
            get
            {
                _type ??= new H5DataType(InternalDataType);

                return _type;
            }
        }

        /// <summary>
        /// Gets the data layout.
        /// </summary>
        public H5DataLayout Layout
        {
            get
            {
                _layout ??= new H5DataLayout(InternalDataLayout);

                return _layout;
            }
        }

        /// <summary>
        /// Gets the fill value.
        /// </summary>
        public H5FillValue FillValue
        {
            get
            {
                _fillValue ??= new H5FillValue(InternalFillValue);

                return _fillValue;
            }
        }

        #endregion

        #region Public

        /// <summary>
        /// Reads the data.
        /// </summary>
        /// <param name="fileSelection">The selection within the source HDF5 dataset.</param>
        /// <param name="memorySelection">The selection within the destination memory.</param>
        /// <param name="memoryDims">The dimensions of the destination memory buffer.</param>
        /// <param name="datasetAccess">The dataset access properties.</param>
        /// <returns>The read data as array of <see cref="byte"/>.</returns>
        public byte[] Read(
            Selection? fileSelection = default,
            Selection? memorySelection = default,
            ulong[]? memoryDims = default,
            H5DatasetAccess datasetAccess = default)
        {
            var result = ReadCoreValueAsync<byte, SyncReader>(
                default,
                default,
                fileSelection,
                memorySelection,
                memoryDims,
                datasetAccess,
                skipShuffle: false).GetAwaiter().GetResult();

            if (result is null)
                throw new Exception("The buffer is null. This should never happen.");

            return result;
        }

        /// <summary>
        /// Reads the data. The type parameter <typeparamref name="T"/> must match the <see langword="unmanaged" /> constraint.
        /// </summary>
        /// <typeparam name="T">The type of the data to read.</typeparam>
        /// <param name="fileSelection">The selection within the source HDF5 dataset.</param>
        /// <param name="memorySelection">The selection within the destination memory.</param>
        /// <param name="memoryDims">The dimensions of the destination memory buffer.</param>
        /// <param name="datasetAccess">The dataset access properties.</param>
        /// <returns>The read data as array of <typeparamref name="T"/>.</returns>
        public T[] Read<T>(
            Selection? fileSelection = default,
            Selection? memorySelection = default,
            ulong[]? memoryDims = default,
            H5DatasetAccess datasetAccess = default) where T : unmanaged
        {
            var result = ReadCoreValueAsync<T, SyncReader>(
                default,
                default,
                fileSelection,
                memorySelection,
                memoryDims,
                datasetAccess,
                skipShuffle: false).GetAwaiter().GetResult();

            if (result is null)
                throw new Exception("The buffer is null. This should never happen.");

            return result;
        }

        /// <summary>
        /// Queries the data. More information: <seealso href="https://github.com/Apollo3zehn/PureHDF#42-experimental-iqueryable-1-dimensional-data-only">PureHDF</seealso>.
        /// </summary>
        /// <typeparam name="T">The type of the data to read.</typeparam>
        /// <param name="memorySelection">The selection within the destination memory.</param>
        /// <param name="memoryDims">The dimensions of the destination memory buffer.</param>
        /// <param name="datasetAccess">The dataset access properties.</param>
        /// <returns>A queryable of type <typeparamref name="T"/>.</returns>
        public IQueryable<T> AsQueryable<T>(
            Selection? memorySelection = default,
            ulong[]? memoryDims = default,
            H5DatasetAccess datasetAccess = default) where T : unmanaged
        {
            if (Space.Rank != 1)
                throw new Exception("Querying data only works for 1-dimensional datasets.");

            var provider = new QueryProvider<T>(
                datasetLength: Space.Dimensions[0],
                executor: fileSelection => Read<T>(fileSelection, memorySelection, memoryDims, datasetAccess));

            var queryable = new Queryable<T>(provider);

            return queryable;
        }

        /// <summary>
        /// Reads the data. The type parameter <typeparamref name="T"/> must match the <see langword="unmanaged" /> constraint.
        /// </summary>
        /// <typeparam name="T">The type of the data to read.</typeparam>
        /// <param name="buffer">The destination memory buffer.</param>
        /// <param name="fileSelection">The selection within the source HDF5 dataset.</param>
        /// <param name="memorySelection">The selection within the destination memory.</param>
        /// <param name="memoryDims">The dimensions of the destination memory buffer.</param>
        /// <param name="datasetAccess">The dataset access properties.</param>
        public void Read<T>(
            Memory<T> buffer,
            Selection? fileSelection = default,
            Selection? memorySelection = default,
            ulong[]? memoryDims = default,
            H5DatasetAccess datasetAccess = default) where T : unmanaged
        {
            ReadCoreValueAsync<T, SyncReader>(
                default,
                buffer,
                fileSelection,
                memorySelection,
                memoryDims,
                datasetAccess,
                skipShuffle: false).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Reads the compound data. The type parameter <typeparamref name="T"/> must match the <see langword="struct" /> constraint. Nested fields with nullable references are not supported.
        /// </summary>
        /// <typeparam name="T">The type of the data to read.</typeparam>
        /// <param name="getName">An optional function to map the field names of <typeparamref name="T"/> to the member names of the HDF5 compound type.</param>
        /// <param name="fileSelection">The selection within the source HDF5 dataset.</param>
        /// <param name="memorySelection">The selection within the destination memory.</param>
        /// <param name="memoryDims">The dimensions of the destination memory buffer.</param>
        /// <param name="datasetAccess">The dataset access properties.</param>
        /// <returns>The read data as array of <typeparamref name="T"/>.</returns>
        public T[] ReadCompound<T>(
           Func<FieldInfo, string>? getName = default,
           Selection? fileSelection = default,
           Selection? memorySelection = default,
           ulong[]? memoryDims = default,
           H5DatasetAccess datasetAccess = default) where T : struct
        {
            getName ??= fieldInfo => fieldInfo.Name;

            var data = ReadCoreReferenceAsync<T, SyncReader>(
                default,
                default,
                (source, destination) => ReadUtils.ReadCompound(Context, InternalDataType, source.Span, destination, getName),
                fileSelection,
                memorySelection,
                memoryDims,
                datasetAccess,
                skipShuffle: false).GetAwaiter().GetResult();

            if (data is null)
                throw new Exception("The buffer is null. This should never happen.");

            return data;
        }

        /// <summary>
        /// Reads the compound data. This is the slowest but most flexible option to read compound data as no prior type knowledge is required.
        /// </summary>
        /// <param name="fileSelection">The selection within the source HDF5 dataset.</param>
        /// <param name="memorySelection">The selection within the destination memory.</param>
        /// <param name="memoryDims">The dimensions of the destination memory buffer.</param>
        /// <param name="datasetAccess">The dataset access properties.</param>
        /// <returns>The read data as array of a dictionary with the keys corresponding to the compound member names and the values being the member data.</returns>
        public Dictionary<string, object?>[] ReadCompound(
           Selection? fileSelection = default,
           Selection? memorySelection = default,
           ulong[]? memoryDims = default,
           H5DatasetAccess datasetAccess = default)
        {
            var data = ReadCoreReferenceAsync<Dictionary<string, object?>, SyncReader>(
                default,
                default,
                (source, destination) => ReadUtils.ReadCompound(Context, InternalDataType, source.Span, destination),
                fileSelection,
                memorySelection,
                memoryDims,
                datasetAccess,
                skipShuffle: false).GetAwaiter().GetResult();

            if (data is null)
                throw new Exception("The buffer is null. This should never happen.");

            return data;
        }

// TODO: Unify names: result, destination, destination

        /// <summary>
        /// Reads the string data.
        /// </summary>
        /// <param name="fileSelection">The selection within the source HDF5 dataset.</param>
        /// <param name="memorySelection">The selection within the destination memory.</param>
        /// <param name="memoryDims">The dimensions of the destination memory buffer.</param>
        /// <param name="datasetAccess">The dataset access properties.</param>
        /// <returns>The read data as array of <see cref="string"/>.</returns>
        public string?[] ReadString(
            Selection? fileSelection = default,
            Selection? memorySelection = default,
            ulong[]? memoryDims = default,
            H5DatasetAccess datasetAccess = default)
        {
            var result = ReadCoreReferenceAsync<string, SyncReader>(
                default,
                default,
                (source, destination) => ReadUtils.ReadString(Context, InternalDataType, source.Span, destination),
                fileSelection,
                memorySelection,
                memoryDims,
                datasetAccess,
                skipShuffle: false).GetAwaiter().GetResult();

            if (result is null)
                throw new Exception("The buffer is null. This should never happen.");

            return result;
        }

        #endregion

#if NET6_0_OR_GREATER

        /// <summary>
        /// Reads the data asynchronously. More information: <seealso href="https://github.com/Apollo3zehn/PureHDF#8-asynchronous-data-access-net-6">PureHDF</seealso>.
        /// </summary>
        /// <param name="fileSelection">The selection within the source HDF5 dataset.</param>
        /// <param name="memorySelection">The selection within the destination memory.</param>
        /// <param name="memoryDims">The dimensions of the destination memory buffer.</param>
        /// <param name="datasetAccess">The dataset access properties.</param>
        /// <returns>A task which returns the read data as array of <see cref="byte"/>.</returns>
        public async Task<byte[]> ReadAsync(
            Selection? fileSelection = default,
            Selection? memorySelection = default,
            ulong[]? memoryDims = default,
            H5DatasetAccess datasetAccess = default)
        {
            var result = await ReadCoreValueAsync<byte, AsyncReader>(
                default,
                default,
                fileSelection,
                memorySelection,
                memoryDims,
                datasetAccess,
                skipShuffle: false);

            if (result is null)
                throw new Exception("The buffer is null. This should never happen.");

            return result;
        }

        /// <summary>
        /// Reads the data asynchronously. More information: <seealso href="https://github.com/Apollo3zehn/PureHDF#8-asynchronous-data-access-net-6">PureHDF</seealso>. The type parameter <typeparamref name="T"/> must match the <see langword="unmanaged" /> constraint.
        /// </summary>
        /// <typeparam name="T">The type of the data to read.</typeparam>
        /// <param name="fileSelection">The selection within the source HDF5 dataset.</param>
        /// <param name="memorySelection">The selection within the destination memory.</param>
        /// <param name="memoryDims">The dimensions of the destination memory buffer.</param>
        /// <param name="datasetAccess">The dataset access properties.</param>
        /// <returns>A task which returns the read data as array of <typeparamref name="T"/>.</returns>
        public async Task<T[]> ReadAsync<T>(
            Selection? fileSelection = default,
            Selection? memorySelection = default,
            ulong[]? memoryDims = default,
            H5DatasetAccess datasetAccess = default) where T : unmanaged
        {
            var result = await ReadCoreValueAsync<T, AsyncReader>(
                default,
                default,
                fileSelection,
                memorySelection,
                memoryDims,
                datasetAccess,
                skipShuffle: false);

            if (result is null)
                throw new Exception("The buffer is null. This should never happen.");

            return result;
        }

        /// <summary>
        /// Reads the data asynchronously. More information: <seealso href="https://github.com/Apollo3zehn/PureHDF#8-asynchronous-data-access-net-6">PureHDF</seealso>. The type parameter <typeparamref name="T"/> must match the <see langword="unmanaged" /> constraint.
        /// </summary>
        /// <typeparam name="T">The type of the data to read.</typeparam>
        /// <param name="buffer">The destination memory buffer.</param>
        /// <param name="fileSelection">The selection within the source HDF5 dataset.</param>
        /// <param name="memorySelection">The selection within the destination memory.</param>
        /// <param name="memoryDims">The dimensions of the destination memory buffer.</param>
        /// <param name="datasetAccess">The dataset access properties.</param>
        public Task ReadAsync<T>(
            Memory<T> buffer,
            Selection? fileSelection = default,
            Selection? memorySelection = default,
            ulong[]? memoryDims = default,
            H5DatasetAccess datasetAccess = default) where T : unmanaged
        {
            return ReadCoreValueAsync<T, AsyncReader>(
                default,
                buffer,
                fileSelection,
                memorySelection,
                memoryDims,
                datasetAccess,
                skipShuffle: false);
        }

        /// <summary>
        /// Reads the compound data asynchronously. More information: <seealso href="https://github.com/Apollo3zehn/PureHDF#8-asynchronous-data-access-net-6">PureHDF</seealso>. The type parameter <typeparamref name="T"/> must match the <see langword="struct" /> constraint. Nested fields with nullable references are not supported.
        /// </summary>
        /// <typeparam name="T">The type of the data to read.</typeparam>
        /// <param name="getName">An optional function to map the field names of <typeparamref name="T"/> to the member names of the HDF5 compound type.</param>
        /// <param name="fileSelection">The selection within the source HDF5 dataset.</param>
        /// <param name="memorySelection">The selection within the destination memory.</param>
        /// <param name="memoryDims">The dimensions of the destination memory buffer.</param>
        /// <param name="datasetAccess">The dataset access properties.</param>
        /// <returns>A task which returns the read data as array of <typeparamref name="T"/>.</returns>
        public async Task<T[]> ReadCompoundAsync<T>(
           Func<FieldInfo, string>? getName = default,
           Selection? fileSelection = default,
           Selection? memorySelection = default,
           ulong[]? memoryDims = default,
           H5DatasetAccess datasetAccess = default) where T : struct
        {
            getName ??= fieldInfo => fieldInfo.Name;

            var result = await ReadCoreReferenceAsync<T, AsyncReader>(
                default,
                default,
                (source, destination) => ReadUtils.ReadCompound(Context, InternalDataType, source.Span, destination, getName),
                fileSelection,
                memorySelection,
                memoryDims,
                datasetAccess,
                skipShuffle: false);

            if (result is null)
                throw new Exception("The buffer is null. This should never happen.");

            return result;
        }

        /// <summary>
        /// Reads the compound data asynchronously. More information: <seealso href="https://github.com/Apollo3zehn/PureHDF#8-asynchronous-data-access-net-6">PureHDF</seealso>. This is the slowest but most flexible option to read compound data as no prior type knowledge is required.
        /// </summary>
        /// <param name="fileSelection">The selection within the source HDF5 dataset.</param>
        /// <param name="memorySelection">The selection within the destination memory.</param>
        /// <param name="memoryDims">The dimensions of the destination memory buffer.</param>
        /// <param name="datasetAccess">The dataset access properties.</param>
        /// <returns>A task which returns the read data as array of a dictionary with the keys corresponding to the compound member names and the values being the member data.</returns>
        public async Task<Dictionary<string, object?>[]> ReadCompoundAsync(
           Selection? fileSelection = default,
           Selection? memorySelection = default,
           ulong[]? memoryDims = default,
           H5DatasetAccess datasetAccess = default)
        {
            var data = await ReadCoreReferenceAsync<Dictionary<string, object?>, AsyncReader>(
                default,
                default,
                (source, destination) => ReadUtils.ReadCompound(Context, InternalDataType, source.Span, destination),
                fileSelection,
                memorySelection,
                memoryDims,
                datasetAccess,
                skipShuffle: false);

            if (data is null)
                throw new Exception("The buffer is null. This should never happen.");

            return data;
        }

        /// <summary>
        /// Reads the string data asynchronously. More information: <seealso href="https://github.com/Apollo3zehn/PureHDF#8-asynchronous-data-access-net-6">PureHDF</seealso>.
        /// </summary>
        /// <param name="fileSelection">The selection within the source HDF5 dataset.</param>
        /// <param name="memorySelection">The selection within the destination memory.</param>
        /// <param name="memoryDims">The dimensions of the destination memory buffer.</param>
        /// <param name="datasetAccess">The dataset access properties.</param>
        /// <returns>A task which returns the read data as array of <see cref="string"/>.</returns>
        public async Task<string?[]> ReadStringAsync(
            Selection? fileSelection = default,
            Selection? memorySelection = default,
            ulong[]? memoryDims = default,
            H5DatasetAccess datasetAccess = default)
        {
            var result = await ReadCoreReferenceAsync<string, AsyncReader>(
                default,
                default,
                (source, destination) => ReadUtils.ReadString(Context, InternalDataType, source.Span, destination),
                fileSelection,
                memorySelection,
                memoryDims,
                datasetAccess,
                skipShuffle: false);

            if (result is null)
                throw new Exception("The buffer is null. This should never happen.");

            return result;
        }
#endif
    }
}
