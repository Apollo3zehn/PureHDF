using PureHDF.Selections;
using System.Buffers;
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
        // if (Space.Rank != 1)
        //     throw new Exception("Querying data only works for 1-dimensional datasets.");

        // var provider = new QueryProvider<T>(
        //     datasetLength: Space.Dimensions[0],
        //     executor: fileSelection => Read<T>(datasetAccess, fileSelection, memorySelection, memoryDims));

        // var queryable = new Queryable<T>(provider);

        // return queryable;

        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public T Read<T>(
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
    public T Read<T>(
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
    public Task<T> ReadAsync<T>(
        Selection? fileSelection = null, 
        Selection? memorySelection = null, 
        ulong[]? memoryDims = null,
        CancellationToken cancellationToken = default) where T : unmanaged
    {
        return ReadAsync<T>(default, fileSelection, memorySelection, memoryDims, cancellationToken);
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
    public async Task<T> ReadAsync<T>(
        H5DatasetAccess datasetAccess,
        Selection? fileSelection = default,
        Selection? memorySelection = default,
        ulong[]? memoryDims = default,
        CancellationToken cancellationToken = default) where T : unmanaged
    {
        var (elementType, _) = WriteUtils.GetElementType(typeof(T));

        // TODO cache this
        var method = _methodInfoReadCoreValuePreAsync.MakeGenericMethod(typeof(T), elementType, typeof(AsyncReader));

        var result = await (Task<T>)method.Invoke(this, new object?[] 
        {
            default(AsyncReader),
            fileSelection,
            memorySelection,
            memoryDims,
            datasetAccess,
            false /* skip shuffle */
        });

        return result;
    }

    internal Task<TResult> ReadCoreValueAsyncPre<TResult, TElement, TReader>(
        TReader reader,
        Selection? fileSelection = default,
        Selection? memorySelection = default,
        ulong[]? memoryDims = default,
        H5DatasetAccess datasetAccess = default,
        bool skipShuffle = false)
            where TReader : IReader
    {
        var result = ReadCoreValueAsync<TElement, TReader>(
            reader,
            fileSelection,
            memorySelection,
            memoryDims,
            datasetAccess,
            skipShuffle
        );

        # TODO: convert here from TElement[] to T!
        # error continue here

        return result;
    }

    internal async Task<TElement[]> ReadCoreValueAsync<TElement, TReader>(
        TReader reader,
        Selection? fileSelection = default,
        Selection? memorySelection = default,
        ulong[]? memoryDims = default,
        H5DatasetAccess datasetAccess = default,
        bool skipShuffle = false)
            where TReader : IReader
    {
        var decoder = InternalDataType.GetDecodeInfo<TElement>(Context);

        Task readVirtualDelegate(NativeDataset dataset, Memory<TElement> destination, Selection fileSelection, H5DatasetAccess datasetAccess)
            => dataset.ReadCoreValueAsync<TElement, TReader>(
                reader,
                fileSelection: fileSelection,
                datasetAccess: datasetAccess);

        var fillValue = default(TElement);
        // var fillValue = InternalFillValue.Value is null
        //     ? default
        //     : MemoryMarshal.Cast<byte, TResult>(InternalFillValue.Value)[0];

        var result = await ReadCoreAsync(
            reader,
            decoder,
            readVirtualDelegate,
            fillValue,
            fileSelection,
            memorySelection,
            memoryDims,
            datasetAccess,
            skipShuffle: skipShuffle
        ).ConfigureAwait(false);

        // /* ensure correct endianness */
        // if (InternalDataType.BitField is IByteOrderAware byteOrderAware)
        // {
        //     DataUtils.EnsureEndianness(
        //         source: MemoryMarshal.AsBytes(result.AsSpan()).ToArray() /* make copy of array */,
        //         destination: MemoryMarshal.AsBytes(result.AsSpan()),
        //         byteOrderAware.ByteOrder,
        //         InternalDataType.Size);
        // }

        return result;
    }

    internal async Task<TElement[]> ReadCoreAsync<TElement, TReader>(
        TReader reader,
        DecodeDelegate<TElement> decoder,
        ReadVirtualDelegate<TElement> readVirtualDelegate,
        TElement? fillValue,
        Selection? fileSelection = default,
        Selection? memorySelection = default,
        ulong[]? memoryDims = default,
        H5DatasetAccess datasetAccess = default,
        bool skipShuffle = false)
            where TReader : IReader
    {
        // fast path for null dataspace
        if (InternalDataspace.Type == DataspaceType.Null)
            return Array.Empty<TElement>();

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
            LayoutClass.VirtualStorage => new H5D_Virtual<TElement>(Context, default!, datasetInfo, datasetAccess, fillValue, readVirtualDelegate),

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
        var targetElementCount = MathUtils.CalculateSize(memoryDims);
        var targetMemory = new TElement[targetElementCount];

        /* decode info */
        var decodeInfo = new DecodeInfo<TElement>(
            datasetDims,
            datasetChunkDims,
            memoryDims,
            memoryDims,
            fileSelection,
            memorySelection,
            GetSourceStreamAsync: chunkIndices => h5d.GetReadStreamAsync(reader, chunkIndices),
            GetTargetBuffer: _ => targetMemory,
            Decoder: decoder,
            SourceTypeSize: (int)InternalDataType.Size
        );

        await SelectionUtils
            .DecodeAsync(
                reader, 
                datasetChunkDims.Length, 
                memoryDims.Length, 
                decodeInfo)
            .ConfigureAwait(false);

        return targetMemory;
    }

    #endregion
}