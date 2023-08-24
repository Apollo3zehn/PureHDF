using PureHDF.Selections;
using System.Buffers;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace PureHDF.VOL.Native;

/// <summary>
/// A native HDF5 dataset.
/// </summary>
public class NativeDataset : NativeAttributableObject, IH5Dataset
{
    #region Fields

    private IH5Dataspace? _space;
    private IH5DataType? _type;
    private IH5DataLayout? _layout;
    private NativeFillValue? _fillValue;

    #endregion

    #region Constructors

    internal NativeDataset(NativeReadContext context, NativeNamedReference reference, ObjectHeader header)
        : base(context, reference, header)
    {
        foreach (var message in Header.HeaderMessages)
        {
            var type = message.Data.GetType();

            if (typeof(DataLayoutMessage).IsAssignableFrom(type))
                InternalDataLayout = (DataLayoutMessage)message.Data;

            else if (type == typeof(DataspaceMessage))
                InternalDataspace = (DataspaceMessage)message.Data;

            else if (type == typeof(DatatypeMessage))
                InternalDataType = (DatatypeMessage)message.Data;

            else if (type == typeof(FillValueMessage))
                InternalFillValue = (FillValueMessage)message.Data;

            else if (type == typeof(FilterPipelineMessage))
                InternalFilterPipeline = (FilterPipelineMessage)message.Data;

            else if (type == typeof(ObjectModificationMessage))
                InternalObjectModification = (ObjectModificationMessage)message.Data;

            else if (type == typeof(ExternalFileListMessage))
                InternalExternalFileList = (ExternalFileListMessage)message.Data;
        }

        // check that required fields are set
        if (InternalDataLayout is null)
            throw new Exception("The data layout message is missing.");

        if (InternalDataspace is null)
            throw new Exception("The dataspace message is missing.");

        if (InternalDataType is null)
            throw new Exception("The data type message is missing.");

        InternalElementDataType = InternalDataType.Properties.FirstOrDefault() switch
        {
            ArrayPropertyDescription array => array.BaseType,
            _ => InternalDataType
        };

        // https://github.com/Apollo3zehn/PureHDF/issues/25
        if (InternalFillValue is null)
        {
            // The OldFillValueMessage is optional and so there might be no fill value
            // message at all although the newer message is being marked as required. The
            // workaround is to instantiate a new FillValueMessage with sensible defaults.
            // It is not 100% clear if these defaults are fine.

            var allocationTime = InternalDataLayout.LayoutClass == LayoutClass.Chunked
                ? SpaceAllocationTime.Incremental
                : SpaceAllocationTime.Late;

            InternalFillValue = FillValueMessage.Decode(allocationTime);
        }
    }

    #endregion

    #region Properties

    /// <inheritdoc />
    public IH5Dataspace Space
    {
        get
        {
            _space ??= new NativeDataspace(InternalDataspace);

            return _space;
        }
    }

    /// <inheritdoc />
    public IH5DataType Type
    {
        get
        {
            _type ??= new NativeDataType(InternalDataType);

            return _type;
        }
    }

    /// <inheritdoc />
    public IH5DataLayout Layout
    {
        get
        {
            _layout ??= new NativeDataLayout(InternalDataLayout);

            return _layout;
        }
    }

    /// <inheritdoc />
    public IH5FillValue FillValue
    {
        get
        {
            _fillValue ??= new NativeFillValue(InternalFillValue);

            return _fillValue;
        }
    }

    internal DataLayoutMessage InternalDataLayout { get; }

    internal DataspaceMessage InternalDataspace { get; }

    internal DatatypeMessage InternalDataType { get; }

    internal DatatypeMessage InternalElementDataType { get; }

    internal FillValueMessage InternalFillValue { get; } = default!;

    internal FilterPipelineMessage? InternalFilterPipeline { get; private set; }

    internal ObjectModificationMessage? InternalObjectModification { get; }

    internal ExternalFileListMessage? InternalExternalFileList { get; }

    #endregion

    #region Methods

    /// <summary>
    /// Queries the data. More information: <seealso href="https://github.com/Apollo3zehn/PureHDF#43-experimental-iqueryable-1-dimensional-data-only">PureHDF</seealso>.
    /// </summary>
    /// <typeparam name="T">The type of the data to read.</typeparam>
    /// <param name="datasetAccess">The dataset access properties.</param>
    /// <param name="memorySelection">The selection within the destination memory.</param>
    /// <param name="memoryDims">The dimensions of the destination memory buffer.</param>
    /// <returns>A queryable of type <typeparamref name="T"/>.</returns>
    public IQueryable<T> AsQueryable<T>(
        H5DatasetAccess datasetAccess,
        Selection? memorySelection = default,
        ulong[]? memoryDims = default) where T : unmanaged
    {
        if (Space.Rank != 1)
            throw new Exception("Querying data only works for 1-dimensional datasets.");

        var provider = new QueryProvider<T>(
            datasetLength: Space.Dimensions[0],
            executor: fileSelection => Read<T>(datasetAccess, fileSelection, memorySelection, memoryDims));

        var queryable = new Queryable<T>(provider);

        return queryable;
    }

    /// <inheritdoc />
    public byte[] Read(
        Selection? fileSelection = null, 
        Selection? memorySelection = null, 
        ulong[]? memoryDims = null)
    {
        return Read(default, fileSelection, memorySelection, memoryDims);
    }

    /// <summary>
    /// Reads the data.
    /// </summary>
    /// <param name="fileSelection">The selection within the source HDF5 dataset.</param>
    /// <param name="memorySelection">The selection within the destination memory.</param>
    /// <param name="memoryDims">The dimensions of the destination memory buffer.</param>
    /// <param name="datasetAccess">The dataset access properties.</param>
    /// <returns>The read data as array of <see cref="byte"/>.</returns>
    public byte[] Read(
        H5DatasetAccess datasetAccess,
        Selection? fileSelection = default,
        Selection? memorySelection = default,
        ulong[]? memoryDims = default)
    {
        var result = ReadCoreValueAsync<byte, SyncReader>(
            default,
            default,
            fileSelection,
            memorySelection,
            memoryDims,
            datasetAccess,
            skipShuffle: false).GetAwaiter().GetResult() 
            ?? throw new Exception("The buffer is null. This should never happen.");

        return result;
    }

    /// <inheritdoc />
    public T[] Read<T>(
        Selection? fileSelection = null, 
        Selection? memorySelection = null, 
        ulong[]? memoryDims = null) where T : unmanaged
    {
        return Read<T>(default(H5DatasetAccess), fileSelection, memorySelection, memoryDims);
    }

    /// <summary>
    /// Reads the data. The type parameter <typeparamref name="T"/> must match the <see langword="unmanaged" /> constraint.
    /// </summary>
    /// <typeparam name="T">The type of the data to read.</typeparam>
    /// <param name="datasetAccess">The dataset access properties.</param>
    /// <param name="fileSelection">The selection within the source HDF5 dataset.</param>
    /// <param name="memorySelection">The selection within the destination memory.</param>
    /// <param name="memoryDims">The dimensions of the destination memory buffer.</param>
    /// <returns>The read data as array of <typeparamref name="T"/>.</returns>
    public T[] Read<T>(
        H5DatasetAccess datasetAccess,
        Selection? fileSelection = default,
        Selection? memorySelection = default,
        ulong[]? memoryDims = default) where T : unmanaged
    {
        var result = ReadCoreValueAsync<T, SyncReader>(
            default,
            default,
            fileSelection,
            memorySelection,
            memoryDims,
            datasetAccess,
            skipShuffle: false).GetAwaiter().GetResult() 
                ?? throw new Exception("The buffer is null. This should never happen.");
        return result;
    }

    /// <inheritdoc />
    public void Read<T>(
        Memory<T> buffer, 
        Selection? fileSelection = null, 
        Selection? memorySelection = null, 
        ulong[]? memoryDims = null) where T : unmanaged
    {
        Read(buffer, default, fileSelection, memorySelection, memoryDims);
    }

    /// <summary>
    /// Reads the data. The type parameter <typeparamref name="T"/> must match the <see langword="unmanaged" /> constraint.
    /// </summary>
    /// <typeparam name="T">The type of the data to read.</typeparam>
    /// <param name="buffer">The destination memory buffer.</param>
    /// <param name="datasetAccess">The dataset access properties.</param>
    /// <param name="fileSelection">The selection within the source HDF5 dataset.</param>
    /// <param name="memorySelection">The selection within the destination memory.</param>
    /// <param name="memoryDims">The dimensions of the destination memory buffer.</param>
    public void Read<T>(
        Memory<T> buffer,
        H5DatasetAccess datasetAccess,
        Selection? fileSelection = default,
        Selection? memorySelection = default,
        ulong[]? memoryDims = default) where T : unmanaged
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

    /// <inheritdoc />
    public T[] ReadCompound<T>(
        Func<FieldInfo, string?>? getName = null, 
        Selection? fileSelection = null, 
        Selection? memorySelection = null, 
        ulong[]? memoryDims = null) where T : struct
    {
        return ReadCompound<T>(default, getName, fileSelection, memorySelection, memoryDims);
    }

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
    public T[] ReadCompound<T>(
        H5DatasetAccess datasetAccess,
        Func<FieldInfo, string?>? getName = default,
        Selection? fileSelection = default,
        Selection? memorySelection = default,
        ulong[]? memoryDims = default) where T : struct
    {
        getName ??= fieldInfo => fieldInfo.Name;

        var data = ReadCoreReferenceAsync<T, SyncReader>(
            default,
            default,
            (source, destination) => ReadUtils.ReadCompound(Context, InternalElementDataType, source.Span, destination, getName),
            fileSelection,
            memorySelection,
            memoryDims,
            datasetAccess,
            skipShuffle: false).GetAwaiter().GetResult() 
                ?? throw new Exception("The buffer is null. This should never happen.");

        return data;
    }

    /// <inheritdoc />
    public Dictionary<string, object?>[] ReadCompound(
        Selection? fileSelection = null, 
        Selection? memorySelection = null,
        ulong[]? memoryDims = null)
    {
        return ReadCompound(default, fileSelection, memorySelection, memoryDims);
    }

    /// <summary>
    /// Reads the compound data. This is the slowest but most flexible option to read compound data as no prior type knowledge is required.
    /// </summary>
    /// <param name="datasetAccess">The dataset access properties.</param>
    /// <param name="fileSelection">The selection within the source HDF5 dataset.</param>
    /// <param name="memorySelection">The selection within the target memory.</param>
    /// <param name="memoryDims">The dimensions of the target memory buffer.</param>
    /// <returns>The read data as array of a dictionary with the keys corresponding to the compound member names and the values being the member data.</returns>
    public Dictionary<string, object?>[] ReadCompound(
        H5DatasetAccess datasetAccess,
        Selection? fileSelection = default,
        Selection? memorySelection = default,
        ulong[]? memoryDims = default)
    {
        var data = ReadCoreReferenceAsync<Dictionary<string, object?>, SyncReader>(
            default,
            default,
            (source, destination) => ReadUtils.ReadCompound(Context, InternalElementDataType, source.Span, destination),
            fileSelection,
            memorySelection,
            memoryDims,
            datasetAccess,
            skipShuffle: false).GetAwaiter().GetResult() 
                ?? throw new Exception("The buffer is null. This should never happen.");

        return data;
    }

    /// <inheritdoc />
    public string?[] ReadString(
        Selection? fileSelection = null, 
        Selection? memorySelection = null, 
        ulong[]? memoryDims = null)
    {
        return ReadString(default, fileSelection, memorySelection, memoryDims);
    }

    // TODO: Unify names: result, destination, destination

    /// <summary>
    /// Reads the string data.
    /// </summary>
    /// <param name="datasetAccess">The dataset access properties.</param>
    /// <param name="fileSelection">The selection within the source HDF5 dataset.</param>
    /// <param name="memorySelection">The selection within the destination memory.</param>
    /// <param name="memoryDims">The dimensions of the destination memory buffer.</param>
    /// <returns>The read data as array of <see cref="string"/>.</returns>
    public string?[] ReadString(
        H5DatasetAccess datasetAccess,
        Selection? fileSelection = default,
        Selection? memorySelection = default,
        ulong[]? memoryDims = default)
    {
        var result = ReadCoreReferenceAsync<string, SyncReader>(
            default,
            default,
            (source, destination) => ReadUtils.ReadString(Context, InternalElementDataType, source.Span, destination),
            fileSelection,
            memorySelection,
            memoryDims,
            datasetAccess,
            skipShuffle: false).GetAwaiter().GetResult() 
                ?? throw new Exception("The buffer is null. This should never happen.");

        return result;
    }

    /// <inheritdoc />
    public T[]?[] ReadVariableLength<T>(
        Selection? fileSelection = null, 
        Selection? memorySelection = null, 
        ulong[]? memoryDims = null) where T : struct
    {
        return ReadVariableLength<T>(default, fileSelection, memorySelection, memoryDims);
    }

    /// <summary>
    /// Reads the variable-length sequence data.
    /// </summary>
    /// <param name="datasetAccess">The dataset access properties.</param>
    /// <param name="fileSelection">The selection within the source HDF5 dataset.</param>
    /// <param name="memorySelection">The selection within the destination memory.</param>
    /// <param name="memoryDims">The dimensions of the destination memory buffer.</param>
    /// <returns>The read data as jagged array of <typeparamref name="T"/>.</returns>
    public T[]?[] ReadVariableLength<T>(
        H5DatasetAccess datasetAccess,
        Selection? fileSelection = null,
        Selection? memorySelection = null, 
        ulong[]? memoryDims = null) where T : struct
    {
        var result = ReadCoreReferenceAsync<T[], SyncReader>(
            default,
            default,
            (source, destination) => ReadUtils.ReadVariableLengthSequence(Context, InternalElementDataType, source.Span, destination),
            fileSelection,
            memorySelection,
            memoryDims,
            datasetAccess,
            skipShuffle: false).GetAwaiter().GetResult() 
                ?? throw new Exception("The buffer is null. This should never happen.");

        return result;
    }

    /// <inheritdoc />
    public Task<byte[]> ReadAsync(
        Selection? fileSelection = null, 
        Selection? memorySelection = null, 
        ulong[]? memoryDims = null, 
        CancellationToken cancellationToken = default)
    {
        return ReadAsync(default, fileSelection, memorySelection, memoryDims, cancellationToken);
    }

    /// <summary>
    /// Reads the data asynchronously. More information: <seealso href="https://github.com/Apollo3zehn/PureHDF#8-asynchronous-data-access-net-6">PureHDF</seealso>.
    /// </summary>
    /// <param name="fileSelection">The selection within the source HDF5 dataset.</param>
    /// <param name="memorySelection">The selection within the destination memory.</param>
    /// <param name="memoryDims">The dimensions of the destination memory buffer.</param>
    /// <param name="datasetAccess">The dataset access properties.</param>
    /// <param name="cancellationToken">A token to cancel the current operation.</param>
    /// <returns>A task which returns the read data as array of <see cref="byte"/>.</returns>
    public async Task<byte[]> ReadAsync(
        H5DatasetAccess datasetAccess,
        Selection? fileSelection = default,
        Selection? memorySelection = default,
        ulong[]? memoryDims = default,
        CancellationToken cancellationToken = default)
    {
        var result = await ReadCoreValueAsync<byte, AsyncReader>(
            default,
            default,
            fileSelection,
            memorySelection,
            memoryDims,
            datasetAccess,
            skipShuffle: false) 
                ?? throw new Exception("The buffer is null. This should never happen.");

        return result;
    }

    /// <inheritdoc />
    public Task<T[]> ReadAsync<T>(
        Selection? fileSelection = null, 
        Selection? memorySelection = null, 
        ulong[]? memoryDims = null,
        CancellationToken cancellationToken = default) where T : unmanaged
    {
        return ReadAsync<T>(default(H5DatasetAccess), fileSelection, memorySelection, memoryDims, cancellationToken);
    }

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
    public async Task<T[]> ReadAsync<T>(
        H5DatasetAccess datasetAccess,
        Selection? fileSelection = default,
        Selection? memorySelection = default,
        ulong[]? memoryDims = default,
        CancellationToken cancellationToken = default) where T : unmanaged
    {
        var result = await ReadCoreValueAsync<T, AsyncReader>(
            default,
            default,
            fileSelection,
            memorySelection,
            memoryDims,
            datasetAccess,
            skipShuffle: false) 
            ?? throw new Exception("The buffer is null. This should never happen.");

        return result;
    }

    /// <inheritdoc />
    public Task ReadAsync<T>(
        Memory<T> buffer, 
        Selection? fileSelection = null, 
        Selection? memorySelection = null, 
        ulong[]? memoryDims = null, 
        CancellationToken cancellationToken = default) where T : unmanaged
    {
        return ReadAsync(buffer, default, fileSelection, memorySelection, memoryDims, cancellationToken);
    }

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
    public Task ReadAsync<T>(
        Memory<T> buffer,
        H5DatasetAccess datasetAccess,
        Selection? fileSelection = default,
        Selection? memorySelection = default,
        ulong[]? memoryDims = default,
        CancellationToken cancellationToken = default) where T : unmanaged
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

    /// <inheritdoc />
    public Task<T[]> ReadCompoundAsync<T>(
        Func<FieldInfo, string?>? getName = null, 
        Selection? fileSelection = null, 
        Selection? memorySelection = null, 
        ulong[]? memoryDims = null,
        CancellationToken cancellationToken = default) where T : struct
    {
        return ReadCompoundAsync<T>(default, getName, fileSelection, memorySelection, memoryDims, cancellationToken);
    }

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
    /// 
    public async Task<T[]> ReadCompoundAsync<T>(
        H5DatasetAccess datasetAccess,
        Func<FieldInfo, string?>? getName = default,
        Selection? fileSelection = default,
        Selection? memorySelection = default,
        ulong[]? memoryDims = default,
        CancellationToken cancellationToken = default) where T : struct
    {
        getName ??= fieldInfo => fieldInfo.Name;

        var result = await ReadCoreReferenceAsync<T, AsyncReader>(
            default,
            default,
            (source, destination) => ReadUtils.ReadCompound(Context, InternalElementDataType, source.Span, destination, getName),
            fileSelection,
            memorySelection,
            memoryDims,
            datasetAccess,
            skipShuffle: false) 
            ?? throw new Exception("The buffer is null. This should never happen.");

        return result;
    }

    /// <inheritdoc />
    public Task<Dictionary<string, object?>[]> ReadCompoundAsync(
        Selection? fileSelection = null, 
        Selection? memorySelection = null, 
        ulong[]? memoryDims = null,
        CancellationToken cancellationToken = default)
    {
        return ReadCompoundAsync(default, fileSelection, memorySelection, memoryDims, cancellationToken);
    }


    /// <summary>
    /// Reads the compound data asynchronously. More information: <seealso href="https://github.com/Apollo3zehn/PureHDF#8-asynchronous-data-access-net-6">PureHDF</seealso>. This is the slowest but most flexible option to read compound data as no prior type knowledge is required.
    /// </summary>
    /// <param name="datasetAccess">The dataset access properties.</param>
    /// <param name="fileSelection">The selection within the source HDF5 dataset.</param>
    /// <param name="memorySelection">The selection within the destination memory.</param>
    /// <param name="memoryDims">The dimensions of the destination memory buffer.</param>
    /// <param name="cancellationToken">A token to cancel the current operation.</param>
    /// <returns>A task which returns the read data as array of a dictionary with the keys corresponding to the compound member names and the values being the member data.</returns>
    public async Task<Dictionary<string, object?>[]> ReadCompoundAsync(
        H5DatasetAccess datasetAccess,
        Selection? fileSelection = default,
        Selection? memorySelection = default,
        ulong[]? memoryDims = default,
        CancellationToken cancellationToken = default)
    {
        var data = await ReadCoreReferenceAsync<Dictionary<string, object?>, AsyncReader>(
            default,
            default,
            (source, destination) => ReadUtils.ReadCompound(Context, InternalElementDataType, source.Span, destination),
            fileSelection,
            memorySelection,
            memoryDims,
            datasetAccess,
            skipShuffle: false)
            ?? throw new Exception("The buffer is null. This should never happen.");
            
        return data;
    }

    /// <inheritdoc />
    public Task<string?[]> ReadStringAsync(
        Selection? fileSelection = null, 
        Selection? memorySelection = null,
        ulong[]? memoryDims = null,
        CancellationToken cancellationToken = default)
    {
        return ReadStringAsync(default, fileSelection, memorySelection, memoryDims, cancellationToken);
    }

    /// <summary>
    /// Reads the string data asynchronously. More information: <seealso href="https://github.com/Apollo3zehn/PureHDF#8-asynchronous-data-access-net-6">PureHDF</seealso>.
    /// </summary>
    /// <param name="datasetAccess">The dataset access properties.</param>
    /// <param name="fileSelection">The selection within the source HDF5 dataset.</param>
    /// <param name="memorySelection">The selection within the destination memory.</param>
    /// <param name="memoryDims">The dimensions of the destination memory buffer.</param>
    /// <param name="cancellationToken">A token to cancel the current operation.</param>
    /// <returns>A task which returns the read data as array of <see cref="string"/>.</returns>
    public async Task<string?[]> ReadStringAsync(
        H5DatasetAccess datasetAccess,
        Selection? fileSelection = default,
        Selection? memorySelection = default,
        ulong[]? memoryDims = default,
        CancellationToken cancellationToken = default)
    {
        var result = await ReadCoreReferenceAsync<string, AsyncReader>(
            default,
            default,
            (source, destination) => ReadUtils.ReadString(Context, InternalElementDataType, source.Span, destination),
            fileSelection,
            memorySelection,
            memoryDims,
            datasetAccess,
            skipShuffle: false) 
            ?? throw new Exception("The buffer is null. This should never happen.");
            
        return result;
    }

    /// <inheritdoc />
    public Task<T[]?[]> ReadVariableLengthAsync<T>(
        Selection? fileSelection = null, 
        Selection? memorySelection = null, 
        ulong[]? memoryDims = null, 
        CancellationToken cancellationToken = default) where T : struct
    {
        return ReadVariableLengthAsync<T>(default, fileSelection, memorySelection, memoryDims, cancellationToken);
    }

    /// <summary>
    /// Reads the variable-length sequence data asynchronously. More information: <seealso href="https://github.com/Apollo3zehn/PureHDF#8-asynchronous-data-access-net-6">PureHDF</seealso>.
    /// </summary>
    /// <param name="datasetAccess">The dataset access properties.</param>
    /// <param name="fileSelection">The selection within the source HDF5 dataset.</param>
    /// <param name="memorySelection">The selection within the destination memory.</param>
    /// <param name="memoryDims">The dimensions of the destination memory buffer.</param>
    /// <param name="cancellationToken">A token to cancel the current operation.</param>
    /// <returns>A task which returns the read data as jagged array of <typeparamref name="T"/>.</returns>
    public async Task<T[]?[]> ReadVariableLengthAsync<T>(
        H5DatasetAccess datasetAccess,
        Selection? fileSelection = null, 
        Selection? memorySelection = null, 
        ulong[]? memoryDims = null, 
        CancellationToken cancellationToken = default) where T : struct
    {
        var result = await ReadCoreReferenceAsync<T[], AsyncReader>(
            default,
            default,
            (source, destination) => ReadUtils.ReadVariableLengthSequence(Context, InternalElementDataType, source.Span, destination),
            fileSelection,
            memorySelection,
            memoryDims,
            datasetAccess,
            skipShuffle: false) 
            ?? throw new Exception("The buffer is null. This should never happen.");
            
        return result;
    }

    internal async Task<TResult[]?> ReadCoreValueAsync<TResult, TReader>(
        TReader reader,
        Memory<TResult> destination,
        Selection? fileSelection = default,
        Selection? memorySelection = default,
        ulong[]? memoryDims = default,
        H5DatasetAccess datasetAccess = default,
        bool skipShuffle = false)
            where TResult : unmanaged
            where TReader : IReader
    {
        // only allow size of T that matches bytesOfType or size of T = 1
        var sizeOfT = (ulong)Unsafe.SizeOf<TResult>();
        var bytesOfType = InternalDataType.Size;

        if (bytesOfType % sizeOfT != 0)
            throw new Exception("The size of the generic parameter must be a multiple of the HDF5 file internal data type size.");

        var factor = (int)(bytesOfType / sizeOfT);

        static void decoder(Memory<byte> source, Memory<TResult> target)
            => source.Span.CopyTo(MemoryMarshal.AsBytes(target.Span));

        Task readVirtualDelegate(NativeDataset dataset, Memory<TResult> destination, Selection fileSelection, H5DatasetAccess datasetAccess)
            => dataset.ReadCoreValueAsync(
                reader,
                destination,
                fileSelection: fileSelection,
                datasetAccess: datasetAccess);

        var fillValue = InternalFillValue.Value is null
            ? default
            : MemoryMarshal.Cast<byte, TResult>(InternalFillValue.Value)[0];

        var result = await ReadCoreAsync(
            reader,
            destination,
            decoder,
            readVirtualDelegate,
            fillValue,
            factor,
            fileSelection,
            memorySelection,
            memoryDims,
            datasetAccess,
            skipShuffle: skipShuffle
        ).ConfigureAwait(false);

        /* ensure correct endianness */
        if (InternalDataType.BitField is IByteOrderAware byteOrderAware)
        {
            DataUtils.EnsureEndianness(
                source: MemoryMarshal.AsBytes(result.AsSpan()).ToArray() /* make copy of array */,
                destination: MemoryMarshal.AsBytes(result.AsSpan()),
                byteOrderAware.ByteOrder,
                InternalDataType.Size);
        }

        return result;
    }

    internal async Task<TResult[]?> ReadCoreReferenceAsync<TResult, TReader>(
        TReader reader,
        Memory<TResult> destination,
        Action<Memory<byte>, Memory<TResult>> decoder,
        Selection? fileSelection = default,
        Selection? memorySelection = default,
        ulong[]? memoryDims = default,
        H5DatasetAccess datasetAccess = default,
        bool skipShuffle = false)
            where TReader : IReader
    {
        var factor = InternalDataType.Properties.FirstOrDefault() switch
        {
            ArrayPropertyDescription array => (int)MathUtils.CalculateSize(array.DimensionSizes),
            _ => 1
        };

        Task readVirtualDelegate(NativeDataset dataset, Memory<TResult> destination, Selection fileSelection, H5DatasetAccess datasetAccess)
            => dataset.ReadCoreReferenceAsync(
                reader,
                destination,
                decoder,
                fileSelection: fileSelection,
                datasetAccess: datasetAccess);

        using var fillValueArrayOwner = MemoryPool<TResult>.Shared.Rent(1);
        var fillValueArray = fillValueArrayOwner.Memory[..1];
        var fillValue = default(TResult);

        if (InternalFillValue.Value is not null)
        {
            decoder(InternalFillValue.Value, fillValueArray);
            fillValue = fillValueArray.Span[0];
        }

        var result = await ReadCoreAsync(
            reader,
            destination,
            decoder,
            readVirtualDelegate,
            fillValue,
            factor,
            fileSelection,
            memorySelection,
            memoryDims,
            datasetAccess,
            skipShuffle: skipShuffle
        );

        return result!;
    }

    internal async Task<TResult[]?> ReadCoreAsync<TResult, TReader>(
        TReader reader,
        Memory<TResult> destination,
        Action<Memory<byte>, Memory<TResult>> decoder,
        ReadVirtualDelegate<TResult> readVirtualDelegate,
        TResult? fillValue,
        int factor,
        Selection? fileSelection = default,
        Selection? memorySelection = default,
        ulong[]? memoryDims = default,
        H5DatasetAccess datasetAccess = default,
        bool skipShuffle = false)
            where TReader : IReader
    {
        // fast path for null dataspace
        if (InternalDataspace.Type == DataspaceType.Null)
            return Array.Empty<TResult>();

        // for testing only
        if (skipShuffle && InternalFilterPipeline is not null)
        {
            InternalFilterPipeline = InternalFilterPipeline with {
                FilterDescriptions = InternalFilterPipeline.FilterDescriptions
                    .Where(description => description.Identifier != ShuffleFilter.Id)
                    .ToArray()
            };
        }

        /* dataset info */
        var datasetInfo = new DatasetInfo(
            Space: InternalDataspace,
            Type: InternalDataType,
            Layout: InternalDataLayout,
            FillValue: InternalFillValue,
            FilterPipeline: InternalFilterPipeline,
            ExternalFileList: InternalExternalFileList
        );

        /* buffer provider */
        using H5D_Base h5d = InternalDataLayout.LayoutClass switch
        {
            /* Compact: The array is stored in one contiguous block as part of
             * this object header message. 
             */
            LayoutClass.Compact => new H5D_Compact(Context, default!, datasetInfo, datasetAccess),

            /* Contiguous: The array is stored in one contiguous area of the file. 
            * This layout requires that the size of the array be constant: 
            * data manipulations such as chunking, compression, checksums, 
            * or encryption are not permitted. The message stores the total
            * storage size of the array. The offset of an element from the 
            * beginning of the storage area is computed as in a C array.
            */
            LayoutClass.Contiguous => new H5D_Contiguous(Context, default!, datasetInfo, datasetAccess),

            /* Chunked: The array domain is regularly decomposed into chunks,
             * and each chunk is allocated and stored separately. This layout 
             * supports arbitrary element traversals, compression, encryption,
             * and checksums (these features are described in other messages).
             * The message stores the size of a chunk instead of the size of the
             * entire array; the storage size of the entire array can be 
             * calculated by traversing the chunk index that stores the chunk 
             * addresses. 
             */
            LayoutClass.Chunked => H5D_Chunk.Create(Context, default!, datasetInfo, datasetAccess, default),

            /* Virtual: This is only supported for version 4 of the Data Layout 
             * message. The message stores information that is used to locate 
             * the global heap collection containing the Virtual Dataset (VDS) 
             * mapping information. The mapping associates the VDS to the source
             * dataset elements that are stored across a collection of HDF5 files.
             */
            LayoutClass.VirtualStorage => new H5D_Virtual<TResult>(Context, default!, datasetInfo, datasetAccess, fillValue, readVirtualDelegate),

            /* default */
            _ => throw new Exception($"The data layout class '{InternalDataLayout.LayoutClass}' is not supported.")
        };

        h5d.Initialize();

        /* dataset dims */
        var datasetDims = datasetInfo.GetDatasetDims();

        /* dataset chunk dims */
        var datasetChunkDims = h5d.GetChunkDims();

        /* file selection */
        if (fileSelection is null || fileSelection is AllSelection)
        {
            fileSelection = InternalDataspace.Type switch
            {
                DataspaceType.Scalar or DataspaceType.Simple => new HyperslabSelection(
                    rank: datasetDims.Length,
                    starts: new ulong[datasetDims.Length],
                    blocks: datasetDims),

                _ => throw new Exception($"Unsupported data space type '{InternalDataspace.Type}'.")
            };
        }

        /* memory dims */
        var sourceElementCount = fileSelection.TotalElementCount;

        if (memorySelection is not null && memoryDims is null)
            throw new Exception("If a memory selection is specified, the memory dimensions must be specified, too.");

        memoryDims ??= new ulong[] { sourceElementCount };

        /* memory selection */
        memorySelection ??= new HyperslabSelection(
            rank: memoryDims.Length, 
            starts: new ulong[memoryDims.Length], 
            blocks: memoryDims);

        /* target buffer */
        var destinationElementCount = MathUtils.CalculateSize(memoryDims);
        var destinationElementCountScaled = destinationElementCount * (ulong)factor;

        EnsureBuffer(destination, destinationElementCountScaled, out var optionalDestinationArray);
        var destinationMemory = optionalDestinationArray ?? destination;

        /* decode info */
        var decodeInfo = new DecodeInfo<TResult>(
            datasetDims,
            datasetChunkDims,
            memoryDims,
            memoryDims,
            fileSelection,
            memorySelection,
            GetSourceStreamAsync: chunkIndices => h5d.GetReadStreamAsync(reader, chunkIndices),
            GetTargetBuffer: _ => destinationMemory,
            Decoder: decoder,
            SourceTypeSize: (int)InternalDataType.Size,
            TargetTypeFactor: factor
        );

        await SelectionUtils
            .DecodeAsync(
                reader, 
                datasetChunkDims.Length, 
                memoryDims.Length, 
                decodeInfo)
            .ConfigureAwait(false);

        return optionalDestinationArray;
    }

    internal static void EnsureBuffer<TResult>(Memory<TResult> destination, ulong destinationElementCount, out TResult[]? newArray)
    {
        newArray = default;

        // user did not provide buffer
        if (destination.Equals(default))
        {
            // create the buffer
            newArray = new TResult[destinationElementCount];
        }

        // user provided buffer is too small
        else if (destination.Length < (int)destinationElementCount)
        {
            throw new Exception("The provided target buffer is too small.");
        }
    }

    #endregion
}