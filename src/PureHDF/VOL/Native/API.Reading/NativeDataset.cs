using PureHDF.Selections;
using System.Buffers;
using System.Collections.Concurrent;
using System.Reflection;

namespace PureHDF.VOL.Native;

/// <summary>
/// A native HDF5 dataset.
/// </summary>
public class NativeDataset : NativeObject, IH5Dataset
{
    #region Fields

    private static readonly MethodInfo _methodInfoReadCoreLevel1_Generic = typeof(NativeDataset)
        .GetMethod(nameof(ReadCoreLevel1_Generic), BindingFlags.NonPublic | BindingFlags.Instance)!;

	// Delegate type for reads, including an instance parameter.
    // Statically cached keyed by (TResult, TElement).
    //
    // Returns an awaitable, so that one reader serves both Read and ReadAsync: the synchronous
    // overloads block on it at the public boundary, the asynchronous ones await it.
    private delegate ValueTask<TResult?> ReaderDelegate<TResult>(
        NativeDataset @this,
        TResult? buffer,
        Selection? fileSelection,
        Selection? memorySelection,
        ulong[]? memoryDims,
        H5DatasetAccess datasetAccess,
        bool skipShuffle);

    private static readonly ConcurrentDictionary<(Type, Type), Delegate> _readerCache = new();

    private static ReaderDelegate<TResult> GetReader<TResult>(Type elementType)
    {
        return (ReaderDelegate<TResult>)_readerCache.GetOrAdd(
            (typeof(TResult), elementType),
            static key =>
            {
                var method = _methodInfoReadCoreLevel1_Generic
                    .MakeGenericMethod(key.Item1, key.Item2);
                var delegateType = typeof(ReaderDelegate<>).MakeGenericType(key.Item1);
                return method.CreateDelegate(delegateType);
            });
    }

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

    /// <inheritdoc />
    public T Read<T>(
        Selection? fileSelection = null,
        Selection? memorySelection = null,
        ulong[]? memoryDims = null)
    {
        return Read<T>(
            datasetAccess: default,
            fileSelection,
            memorySelection,
            memoryDims);
    }

    /// <inheritdoc />
    public void Read<T>(
        T buffer,
        Selection? fileSelection = null,
        Selection? memorySelection = null,
        ulong[]? memoryDims = null)
    {
        Read(
            datasetAccess: default,
            buffer,
            fileSelection,
            memorySelection,
            memoryDims);
    }

    /// <summary>
    /// Reads the data.
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
        ulong[]? memoryDims = default)
    {
        var (elementType, _) = WriteUtils.GetElementType(typeof(T));
        var reader = GetReader<T>(elementType);

        // Blocks once, at the public boundary. ReadAsync below runs the same reader and awaits it.
        return reader(
            this,
            buffer: default,
            fileSelection,
            memorySelection,
            memoryDims,
            datasetAccess,
            skipShuffle: false)
            .GetAwaiter()
            .GetResult()!;
    }

    /// <summary>
    /// Reads the data into the provided buffer.
    /// </summary>
    /// <typeparam name="T">The type of the data to read.</typeparam>
    /// <param name="datasetAccess">The dataset access properties.</param>
    /// <param name="buffer">The buffer to read the data into.</param>
    /// <param name="fileSelection">The selection within the source HDF5 dataset.</param>
    /// <param name="memorySelection">The selection within the destination memory.</param>
    /// <param name="memoryDims">The dimensions of the destination memory buffer.</param>
    public void Read<T>(
        H5DatasetAccess datasetAccess,
        T buffer,
        Selection? fileSelection = default,
        Selection? memorySelection = default,
        ulong[]? memoryDims = default)
    {
        var (elementType, _) = WriteUtils.GetElementType(typeof(T));
        var reader = GetReader<T>(elementType);

        reader(
            this,
            buffer,
            fileSelection,
            memorySelection,
            memoryDims,
            datasetAccess,
            skipShuffle: false)
            .GetAwaiter()
            .GetResult();
    }

    /* The following two methods are required because Span<T> is not allowed as generic
     * argument and ReadUtils.ToMemory(...) would have trouble to cast generic type to
     * Span<T>. https://github.com/dotnet/csharplang/issues/7608 tracks support for the
     * generic argument issue. These methods could also be moved to `IH5Dataset` but the
     * only effect would be that the HsdsDataset would have to throw a
     * `NotImplementedException`.
     */

    /// <summary>
    /// Reads the data into the provided buffer.
    /// </summary>
    /// <typeparam name="T">The type of the data to read.</typeparam>
    /// <param name="buffer">The buffer to read the data into.</param>
    /// <param name="fileSelection">The selection within the source HDF5 dataset.</param>
    /// <param name="memorySelection">The selection within the destination memory.</param>
    /// <param name="memoryDims">The dimensions of the destination memory buffer.</param>
    public void Read<T>(
        Span<T> buffer,
        Selection? fileSelection = default,
        Selection? memorySelection = default,
        ulong[]? memoryDims = default)
    {
        Read(
            datasetAccess: default,
            buffer,
            fileSelection,
            memorySelection,
            memoryDims
        );
    }

    /// <summary>
    /// Reads the data into the provided buffer.
    /// </summary>
    /// <typeparam name="T">The type of the data to read.</typeparam>
    /// <param name="buffer">The buffer to read the data into.</param>
    /// <param name="fileSelection">The selection within the source HDF5 dataset.</param>
    /// <param name="memorySelection">The selection within the destination memory.</param>
    /// <param name="memoryDims">The dimensions of the destination memory buffer.</param>
    /// <param name="datasetAccess">The dataset access properties.</param>
    public void Read<T>(
        H5DatasetAccess datasetAccess,
        Span<T> buffer,
        Selection? fileSelection = default,
        Selection? memorySelection = default,
        ulong[]? memoryDims = default)
    {
        ReadCoreLevel1(
            buffer,
            fileSelection,
            memorySelection,
            memoryDims,
            datasetAccess,
            /* skip shuffle: */ false 
        );
    }

    /// <inheritdoc />
    public Task<T> ReadAsync<T>(
        Selection? fileSelection = null,
        Selection? memorySelection = null,
        ulong[]? memoryDims = null,
        CancellationToken cancellationToken = default)
    {
        return ReadAsync<T>(
            datasetAccess: default,
            fileSelection,
            memorySelection,
            memoryDims,
            cancellationToken);
    }

    /// <summary>
    /// Reads the data.
    /// </summary>
    /// <typeparam name="T">The type of the data to read.</typeparam>
    /// <param name="datasetAccess">The dataset access properties.</param>
    /// <param name="fileSelection">The selection within the source HDF5 dataset.</param>
    /// <param name="memorySelection">The selection within the destination memory.</param>
    /// <param name="memoryDims">The dimensions of the destination memory buffer.</param>
    /// <param name="cancellationToken">A token to cancel the current operation.</param>
    /// <returns>The read data as array of <typeparamref name="T"/>.</returns>
    public async Task<T> ReadAsync<T>(
        H5DatasetAccess datasetAccess,
        Selection? fileSelection = default,
        Selection? memorySelection = default,
        ulong[]? memoryDims = default,
        CancellationToken cancellationToken = default)
    {
        // Honored at the boundary only. The decode pipeline below does not thread a token through, so
        // this cancels before work starts rather than interrupting a read in flight - but a caller
        // passing an already-cancelled token must not get a completed read back.
        cancellationToken.ThrowIfCancellationRequested();

        var (elementType, _) = WriteUtils.GetElementType(typeof(T));
        var reader = GetReader<T>(elementType);

        return (await reader(
            this,
            buffer: default,
            fileSelection,
            memorySelection,
            memoryDims,
            datasetAccess,
            skipShuffle: false).ConfigureAwait(false))!;
    }

    /// <inheritdoc />
    public Task ReadAsync<T>(
        T buffer,
        Selection? fileSelection = null,
        Selection? memorySelection = null,
        ulong[]? memoryDims = null,
        CancellationToken cancellationToken = default)
    {
        return ReadAsync(
            datasetAccess: default,
            buffer,
            fileSelection,
            memorySelection,
            memoryDims,
            cancellationToken);
    }

    /// <summary>
    /// Reads the data into the provided buffer.
    /// </summary>
    /// <typeparam name="T">The type of the data to read.</typeparam>
    /// <param name="datasetAccess">The dataset access properties.</param>
    /// <param name="buffer">The buffer to read the data into.</param>
    /// <param name="fileSelection">The selection within the source HDF5 dataset.</param>
    /// <param name="memorySelection">The selection within the destination memory.</param>
    /// <param name="memoryDims">The dimensions of the destination memory buffer.</param>
    /// <param name="cancellationToken">A token to cancel the current operation.</param>
    public async Task ReadAsync<T>(
        H5DatasetAccess datasetAccess,
        T buffer,
        Selection? fileSelection = default,
        Selection? memorySelection = default,
        ulong[]? memoryDims = default,
        CancellationToken cancellationToken = default)
    {
        // Honored at the boundary only. The decode pipeline below does not thread a token through, so
        // this cancels before work starts rather than interrupting a read in flight - but a caller
        // passing an already-cancelled token must not get a completed read back.
        cancellationToken.ThrowIfCancellationRequested();

        var (elementType, _) = WriteUtils.GetElementType(typeof(T));
        var reader = GetReader<T>(elementType);

        await reader(
            this,
            buffer,
            fileSelection,
            memorySelection,
            memoryDims,
            datasetAccess,
            skipShuffle: false).ConfigureAwait(false);
    }

    internal async ValueTask<TResult?> ReadCoreLevel1_Generic<TResult, TElement>(
        TResult? buffer,
        Selection? fileSelection = default,
        Selection? memorySelection = default,
        ulong[]? memoryDims = default,
        H5DatasetAccess datasetAccess = default,
        bool skipShuffle = false)
    {
        var resultType = typeof(TResult);
        var isRawMode = typeof(TResult) == typeof(byte[]) || typeof(TResult) == typeof(Memory<byte>);

        (var fileDims, fileSelection) = GetFileDimsAndSelection(
            fileSelection, 
            memorySelection, 
            memoryDims, 
            skipShuffle,
            isRawMode);

        /* result buffer / result array */
        Memory<TElement> resultBuffer;
        var resultArray = default(Array);

        if (buffer is null || buffer.Equals(default(TResult)))
        {
            /* memory dims */
            if (isRawMode)
            {
                memoryDims ??= [fileSelection.TotalElementCount * InternalDataType.Size];
            }

            else if (DataUtils.IsArray(resultType))
            {
                var rank = resultType.GetArrayRank();

                if (rank == 1)
                    memoryDims ??= [fileSelection.TotalElementCount];

                else if (rank == fileDims.Length)
                    memoryDims ??= fileDims;

                else if (memoryDims is null)
                    throw new Exception("The rank of the memory space must match the rank of the file space if no memory dimensions are provided.");
            }
            else
            {
                memoryDims ??= [1];
            }

            /* result buffer */
            resultArray = DataUtils.IsArray(resultType) || DataUtils.IsMemory(resultType)
                ? Array.CreateInstance(typeof(TElement), memoryDims.Select(dim => (int)dim).ToArray())
                : new TResult[1];

            resultBuffer = new ArrayMemoryManager<TElement>(resultArray).Memory;
        }

        else
        {
            /* result buffer */
            (var resultMemoryBuffer, memoryDims) = ReadUtils.ToMemory<TResult, TElement>(buffer);
            resultBuffer = resultMemoryBuffer;
        }

        await ReadCoreLevel3(
            resultBuffer,
            fileSelection,
            ResolveMemorySelection<TElement>(fileSelection, memorySelection, memoryDims, isRawMode),
            fileDims,
            memoryDims,
            isRawMode,
            datasetAccess).ConfigureAwait(false);

        /* return */
        if (DataUtils.IsMemory(resultType))
        {
            return (TResult)(object)resultBuffer;
        }

        else
        {
            return resultArray is null
                ? default
                : ReadUtils.FromArray<TResult, TElement>(resultArray);
        }
    }

    private void ReadCoreLevel1<TElement>(
        Span<TElement> buffer,
        Selection? fileSelection = default,
        Selection? memorySelection = default,
        ulong[]? memoryDims = default,
        H5DatasetAccess datasetAccess = default,
        bool skipShuffle = false)
    {
        var isRawMode = typeof(TElement) == typeof(byte);

        (var fileDims, fileSelection) = GetFileDimsAndSelection(
            fileSelection, 
            memorySelection, 
            memoryDims, 
            skipShuffle,
            isRawMode);

        /* result buffer */
        if (memoryDims is null)
            memoryDims = [(ulong)buffer.Length];

        // The public Read<T>(Span<T>) overload cannot hand its Span to an async decoder (a Span
        // cannot cross an await), so decode into a pooled Memory<TElement> and copy out. The
        // array/Memory overloads pass their buffer through with no copy.
        //
        // The caller's contents are copied IN first: a pooled buffer is recycled rather than zeroed,
        // and a decode that only partially fills it must leave the caller's remaining elements
        // untouched - which is what happened when the caller's own span was decoded into directly.
        using var spanOwner = new ScratchBuffer<TElement>(buffer.Length);
        var resultBuffer = spanOwner.Memory[..buffer.Length];

        buffer.CopyTo(resultBuffer.Span);

        // Blocks: this overload takes a Span, which cannot cross an await, so it has no asynchronous
        // counterpart to defer to. IH5Dataset exposes no Span-based async read either.
        ReadCoreLevel3(
            resultBuffer,
            fileSelection,
            ResolveMemorySelection<TElement>(fileSelection, memorySelection, memoryDims, isRawMode),
            fileDims,
            memoryDims,
            isRawMode,
            datasetAccess)
            .GetAwaiter()
            .GetResult();

        resultBuffer.Span.CopyTo(buffer);
    }

    // Deliberately NOT async, and it no longer calls level 3 - it resolves and validates the memory
    // selection and hands it back, so its caller awaits level 3 directly.
    //
    // The reason is measurable: this defaults a selection and compares two counts, never reads, and an
    // `async` method that completes synchronously still costs a builder and an awaiter check. With
    // three nested async levels a 10,000-iteration scalar read measured ~12% slower than the
    // synchronous path it replaced, at byte-identical allocation - all of it state-machine setup. One
    // level of the three had no reason to be async at all.
    private Selection ResolveMemorySelection<TElement>(
        Selection fileSelection,
        Selection? memorySelection,
        ulong[] memoryDims,
        bool isRawMode)
    {
        /* memory selection */
        if (memorySelection is null || memorySelection is AllSelection)
        {
            memorySelection = new HyperslabSelection(
                rank: memoryDims.Length,
                starts: new ulong[memoryDims.Length],
                blocks: isRawMode
                    ? memoryDims
                        .Select(dim => dim / InternalDataType.Size)
                        .ToArray()
                    : memoryDims
            );
        }

        /* validation */
        if (memorySelection.TotalElementCount != fileSelection.TotalElementCount)
            throw new Exception("The total file selection element count does not match the total memory selection element count.");

        return memorySelection;
    }

    private async ValueTask ReadCoreLevel3<TElement>(
        Memory<TElement> resultBuffer,
        Selection fileSelection,
        Selection memorySelection,
        ulong[] fileDims,
        ulong[] memoryDims,
        bool isRawMode,
        H5DatasetAccess datasetAccess = default)
    {
        // CONCURRENCY: a read operation begins in earnest here, so it gets its own driver and its
        // own context. Everything below must use `operationContext`; the file-level `Context` is
        // shared with concurrent operations and with object navigation, and its driver cursor is a
        // plain field. Two exceptions remain, both pre-existing and both flagged where they occur:
        // the external-file-list local heap and the virtual-dataset source lookup still resolve
        // through the file-level driver.
        using var operationScope = new NativeOperationScope(Context);
        var operationContext = operationScope.Context;

        /* get decoder (succeeds only if decoding is possible) */
        var decoder = InternalDataType.GetDecodeInfo<TElement>(operationContext, isRawMode);

        /* fill value */
        TElement? fillValue;

        if (InternalFillValue.Value is not null)
        {
            // TODO cache this
            var target = new TElement[1];
            var fillValueDecodeStream = new SystemMemoryStream(InternalFillValue.Value);

            await decoder.Invoke(operationContext, fillValueDecodeStream, target).ConfigureAwait(false);
            fillValue = target[0];
        }

        else
        {
            fillValue = default;
        }

        /* read virtual delegate */
        // Reads one source dataset straight into the gather buffer. This used to take a Span, which
        // forced two things it no longer needs: a pooled scratch buffer plus a copy to bridge Span to
        // the Memory<T> that ReadCoreLevel3 wants, and a GetAwaiter().GetResult() - so ReadAsync of a
        // VIRTUAL dataset blocked on every source read. Both are gone now that the gather is awaitable;
        // the destination is the caller's Memory and is written in place.
        static ValueTask readVirtualDelegate(NativeDataset dataset, Memory<TElement> destination, Selection fileSelection, H5DatasetAccess datasetAccess)
        {
            return dataset.ReadCoreLevel3(
                resultBuffer: destination,
                fileSelection: fileSelection,
                memorySelection: new HyperslabSelection(0, (ulong)destination.Length),
                fileDims: dataset.InternalDataspace.GetDims(),
                memoryDims: [(ulong)destination.Length],
                isRawMode: false,
                datasetAccess: datasetAccess);
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
            LayoutClass.Compact => new H5D_Compact(operationContext, default!, datasetInfo, datasetAccess),

            /* Contiguous: The array is stored in one contiguous area of the file. 
            * This layout requires that the size of the array be constant: 
            * data manipulations such as chunking, compression, checksums, 
            * or encryption are not permitted. The message stores the total
            * storage size of the array. The offset of an element from the 
            * beginning of the storage area is computed as in a C array.
            */
            // NOTE: for a dataset with an external file list, H5D_Contiguous builds an
            // ExternalFileListStream. Its local heap used to be decoded through the file-level driver
            // captured by ExternalFileListMessage at navigation time; the message no longer holds a
            // context, so the heap is now decoded through `operationContext` like everything else.
            LayoutClass.Contiguous => new H5D_Contiguous(operationContext, default!, datasetInfo, datasetAccess),

            /* Chunked: The array domain is regularly decomposed into chunks,
             * and each chunk is allocated and stored separately. This layout 
             * supports arbitrary element traversals, compression, encryption,
             * and checksums (these features are described in other messages).
             * The message stores the size of a chunk instead of the size of the
             * entire array; the storage size of the entire array can be 
             * calculated by traversing the chunk index that stores the chunk 
             * addresses. 
             */
            LayoutClass.Chunked => H5D_Chunk.Create(operationContext, default!, datasetInfo, datasetAccess, default),

            /* Virtual: This is only supported for version 4 of the Data Layout 
             * message. The message stores information that is used to locate 
             * the global heap collection containing the Virtual Dataset (VDS) 
             * mapping information. The mapping associates the VDS to the source
             * dataset elements that are stored across a collection of HDF5 files.
             */
            // H5D_Virtual's constructor is private; construction goes through the async static
            // factory `Create` because it performs a driver read (VdsGlobalHeapBlock.Decode).
            //
            // The VDS block is decoded through the operation driver. Resolving the source datasets
            // goes through NativeReadContext.File - i.e. object navigation - which takes a scope of
            // its own per call, so it does not move the shared file-level cursor. Each source read
            // then opens its own operation driver on top.
            LayoutClass.VirtualStorage => await H5D_Virtual<TElement>.Create(operationContext, default!, datasetInfo, datasetAccess, fillValue, readVirtualDelegate).ConfigureAwait(false),

            /* default */
            _ => throw new Exception($"The data layout class '{InternalDataLayout.LayoutClass}' is not supported.")
        };

        h5d.Initialize();

        /* file chunk dimensions */
        var fileChunkDims = h5d.GetChunkDims();

        /* decode */
        var rawModeTargetTypeSizeFactor = isRawMode
            ? InternalDataType.Size
            : 1;

        var decodeInfo = new DecodeInfo<TElement>(
            fileDims,
            fileChunkDims,
            memoryDims,
            memoryDims,
            fileSelection,
            memorySelection,
            GetSourceStream: h5d.GetReadStream,
            Context: operationContext,
            Decoder: decoder,
            SourceTypeSize: (int)InternalDataType.Size,
            TargetTypeSizeFactor: (int)rawModeTargetTypeSizeFactor,
            AllowBulkCopy: InternalDataLayout.LayoutClass != LayoutClass.VirtualStorage
        );

        await SelectionHelper
            .Decode(
                sourceRank: fileDims.Length,
                targetRank: memoryDims.Length,
                decodeInfo,
                resultBuffer)
            .ConfigureAwait(false);
    }

    private (ulong[], Selection) GetFileDimsAndSelection(
        Selection? fileSelection, 
        Selection? memorySelection, 
        ulong[]? memoryDims, 
        bool skipShuffle,
        bool isRawMode)
    {
        /* for testing only */
        if (skipShuffle && InternalFilterPipeline is not null)
        {
            InternalFilterPipeline = InternalFilterPipeline with
            {
                FilterDescriptions = InternalFilterPipeline.FilterDescriptions
                    .Where(description => description.Identifier != ShuffleFilter.Id)
                    .ToArray()
            };
        }

        /* check endianness */
        if (!isRawMode)
        {
            var byteOrderAware = InternalDataType.BitField as IByteOrderAware;

            if (byteOrderAware is not null)
                DataUtils.CheckEndianness(byteOrderAware.ByteOrder);
        }

        /* fast path for null dataspace */
        if (InternalDataspace.Type == DataspaceType.Null)
            throw new Exception("Datasets with null dataspace cannot be read.");

        /* memory selection + dims validation */
        if (memorySelection is not null && memoryDims is null)
            throw new Exception("If a memory selection is specified, the memory dimensions must be specified, too.");

        /* file dimensions */
        var fileDims = InternalDataspace.GetDims();

        /* file selection */
        if (fileSelection is null || fileSelection is AllSelection)
        {
            fileSelection = InternalDataspace.Type switch
            {
                DataspaceType.Scalar or DataspaceType.Simple => new HyperslabSelection(
                    rank: fileDims.Length,
                    starts: new ulong[fileDims.Length],
                    blocks: fileDims),

                _ => throw new Exception($"Unsupported data space type '{InternalDataspace.Type}'.")
            };
        }

        return (fileDims, fileSelection);
    }

    #endregion
}