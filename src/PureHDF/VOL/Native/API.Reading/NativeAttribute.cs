using System.Buffers;
using System.Collections.Concurrent;
using System.Reflection;

namespace PureHDF.VOL.Native;

/// <summary>
/// A native HDF5 attribute.
/// </summary>
public class NativeAttribute : IH5Attribute
{
    #region Fields

    private static readonly MethodInfo _methodInfoReadCoreLevel1_Generic = typeof(NativeAttribute)
        .GetMethod(nameof(ReadCoreLevel1_generic), BindingFlags.NonPublic | BindingFlags.Instance)!;

	// Delegate type for reads, including an instance parameter.
    // Statically cached keyed by (TResult, TElement).
    //
    // Returns an awaitable, so that ONE reader serves both Read and ReadAsync: the synchronous
    // overloads block on it at the public boundary, the asynchronous ones await it. Same shape as
    // NativeDataset.ReaderDelegate and for the same reason - the alternative is a second copy of the
    // decode pipeline.
    private delegate ValueTask<TResult?> ReaderDelegate<TResult>(
        NativeAttribute @this,
        TResult? buffer,
        IH5ReadStream source,
        ulong[]? memoryDims);

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
    private readonly NativeReadContext _context;

    #endregion

    #region Constructors

    internal NativeAttribute(NativeReadContext context, AttributeMessage message)
    {
        _context = context;
        Message = message;

        InternalElementDataType = Message.Datatype.Properties.FirstOrDefault() switch
        {
            ArrayPropertyDescription array => array.BaseType,
            _ => Message.Datatype
        };
    }

    #endregion

    #region Properties

    /// <inheritdoc />
    public string Name => Message.Name;

    /// <inheritdoc />
    public IH5Dataspace Space
    {
        get
        {
            _space ??= new NativeDataspace(Message.Dataspace);

            return _space;
        }
    }

    /// <inheritdoc />
    public IH5DataType Type
    {
        get
        {
            _type ??= new NativeDataType(Message.Datatype);

            return _type;
        }
    }

    internal AttributeMessage Message { get; }

    internal DatatypeMessage InternalElementDataType { get; }

    #endregion

    #region Methods

    /// <inheritdoc />
    public T Read<T>(
        ulong[]? memoryDims = null)
    {
        var (elementType, _) = WriteUtils.GetElementType(typeof(T));
        var reader = GetReader<T>(elementType);
        var source = new SystemMemoryStream(Message.InputData);

        // Blocks once, at the public boundary. ReadAsync below runs the same reader and awaits it.
        // For a fixed-size attribute there is nothing to block ON - the bytes are already in
        // Message.InputData - so this returns without ever suspending.
        return reader(this, buffer: default, source, memoryDims)
            .GetAwaiter()
            .GetResult()!;
    }

    /// <inheritdoc />
    public void Read<T>(
        T buffer,
        ulong[]? memoryDims = null)
    {
        var (elementType, _) = WriteUtils.GetElementType(typeof(T));
        var reader = GetReader<T>(elementType);
        var source = new SystemMemoryStream(Message.InputData);

        reader(this, buffer, source, memoryDims)
            .GetAwaiter()
            .GetResult();
    }

    /// <inheritdoc />
    public async Task<T> ReadAsync<T>(
        ulong[]? memoryDims = null,
        CancellationToken cancellationToken = default)
    {
        // Honored at the boundary only: the decode pipeline does not thread a token through, so this
        // cancels before work starts rather than interrupting a read in flight. A caller passing an
        // already-cancelled token must not get a completed read back regardless.
        cancellationToken.ThrowIfCancellationRequested();

        var (elementType, _) = WriteUtils.GetElementType(typeof(T));
        var reader = GetReader<T>(elementType);
        var source = new SystemMemoryStream(Message.InputData);

        return (await reader(this, buffer: default, source, memoryDims).ConfigureAwait(false))!;
    }

    /// <inheritdoc />
    public async Task ReadAsync<T>(
        T buffer,
        ulong[]? memoryDims = null,
        CancellationToken cancellationToken = default)
    {
        // Honored at the boundary only: the decode pipeline does not thread a token through, so this
        // cancels before work starts rather than interrupting a read in flight. A caller passing an
        // already-cancelled token must not get a completed read back regardless.
        cancellationToken.ThrowIfCancellationRequested();

        var (elementType, _) = WriteUtils.GetElementType(typeof(T));
        var reader = GetReader<T>(elementType);
        var source = new SystemMemoryStream(Message.InputData);

        await reader(this, buffer, source, memoryDims).ConfigureAwait(false);
    }

    /* This overload is required because Span<T> is not allowed as generic argument and
     * ReadUtils.ToMemory(...) would have trouble to cast generic type to Span<T>.
     * https://github.com/dotnet/csharplang/issues/7608 tracks support for the generic
     * argument issue.
     */

    /// <summary>
    /// Reads the data into the provided buffer.
    /// </summary>
    /// <typeparam name="T">The type of the data to read.</typeparam>
    /// <param name="buffer">The buffer to read the data into.</param>
    /// <param name="memoryDims">The dimensions of the destination memory buffer.</param>
    public void Read<T>(
        Span<T> buffer,
        ulong[]? memoryDims = null)
    {
        var source = new SystemMemoryStream(Message.InputData);

        ReadCoreLevel1(
            buffer,
            source,
            memoryDims
        );
    }

    private async ValueTask<TResult?> ReadCoreLevel1_generic<TResult, TElement>(
        TResult? buffer,
        IH5ReadStream source,
        ulong[]? memoryDims = null)
    {
        // CONCURRENCY: see the note on the other ReadCoreLevel1 overload below.
        using var operationScope = new NativeOperationScope(_context);
        var operationContext = operationScope.Context;

        var (decoder, fileElementCount) = GetDecoderAndFileElementCount<TElement>(operationContext);

        /* result buffer / result array */
        Memory<TElement> resultBuffer;
        var resultArray = default(Array);

        if (buffer is null || buffer.Equals(default(TResult)))
        {
            var resultType = typeof(TResult);

            /* memory dims */
            if (DataUtils.IsArray(resultType))
            {
                var rank = resultType.GetArrayRank();

                if (rank == 1)
                    memoryDims ??= [fileElementCount];

                else if (rank == Message.Dataspace.Rank)
                    memoryDims ??= Message.Dataspace.GetDims();

                else if (memoryDims is null)
                    throw new Exception("The rank of the memory space must match the rank of the file space if no memory dimensions are provided.");
            }

            else
            {
                memoryDims ??= [1];
            }

            /* result buffer */
            resultArray = DataUtils.IsArray(resultType)
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

        // The operation scope above is held across this await, which is safe because it is
        // per-OPERATION and not per-thread: the driver it owns belongs to this read alone, so a
        // continuation resuming on another thread still has exclusive use of it.
        await ReadCoreLevel2(operationContext, source, memoryDims, fileElementCount, decoder, resultBuffer)
            .ConfigureAwait(false);

        /* return */
        return resultArray is null
            ? default
            : ReadUtils.FromArray<TResult, TElement>(resultArray);
    }

    private void ReadCoreLevel1<TElement>(
        Span<TElement> buffer,
        IH5ReadStream source,
        ulong[]? memoryDims = null)
    {
        // CONCURRENCY: an attribute's own bytes live in the object header (Message.InputData) and
        // are decoded from a SystemMemoryStream, not from the driver - so at first glance no
        // per-operation driver is needed here. It is: variable-length and reference data store
        // global-heap IDs inline, and the decoder resolves them through
        // NativeCache.GetGlobalHeapObject, which SEEKS AND READS the driver. Two threads reading a
        // string attribute concurrently would race on the shared cursor exactly like a dataset read
        // does. Fixed-size attributes pay one driver allocation and never use it.
        using var operationScope = new NativeOperationScope(_context);
        var operationContext = operationScope.Context;

        var (decoder, fileElementCount) = GetDecoderAndFileElementCount<TElement>(operationContext);

        /* result buffer */
        if (memoryDims is null)
            memoryDims = [(ulong)buffer.Length];

        // The public Read<T>(Span<T>) overload cannot hand its Span to an async decoder (a Span
        // cannot cross an await), so decode into a pooled Memory<TElement> and copy out. This is
        // the unavoidable cost of the Span-based overload; the array/Memory overloads above pass
        // their buffer straight through with no copy.
        // The caller's contents are copied IN first: a pooled buffer is recycled rather than zeroed,
        // so a decode that only partially fills it must not write garbage back over the caller's
        // remaining elements.
        using var owner = new ScratchBuffer<TElement>(buffer.Length);
        var resultBuffer = owner.Memory[..buffer.Length];

        buffer.CopyTo(resultBuffer.Span);

        // BLOCKS, and cannot do otherwise: this overload takes a Span, which is a ref struct and so
        // cannot live across an await (CS4012), which rules out an async counterpart for it. There is
        // therefore no async form of Read<T>(Span<T>) on the public surface, and a caller on a host that
        // cannot block must use one of the array/Memory overloads via ReadAsync instead. Harmless for a
        // fixed-size attribute, which never suspends; a variable-length one really would block here.
        ReadCoreLevel2(operationContext, source, memoryDims, fileElementCount, decoder, resultBuffer)
            .GetAwaiter()
            .GetResult();

        resultBuffer.Span.CopyTo(buffer);
    }

    private static ValueTask ReadCoreLevel2<TElement>(
        NativeReadContext context,
        IH5ReadStream source,
        ulong[] memoryDims,
        ulong fileElementCount,
        DecodeDelegate<TElement> decoder,
        Memory<TElement> resultBuffer)
    {
        /* memory element count */
        var memoryElementCount = memoryDims.Aggregate(1UL, (product, dim) => product * dim);

        /* validation */
        if (memoryElementCount != fileElementCount)
            throw new Exception("The total file element count does not match the total memory element count.");

        /* decode */
        // Not `async`: for a fixed-size attribute the decoder reads only from `source`, a memory stream
        // over bytes already held in the object header, so it completes synchronously and no state
        // machine is built. A variable-length or reference datatype is the case that genuinely
        // suspends - it resolves global heap IDs through the driver.
        return decoder(context, source, resultBuffer);
    }

    private (DecodeDelegate<TElement>, ulong) GetDecoderAndFileElementCount<TElement>(
        NativeReadContext context)
    {
        /* check endianness */
        var byteOrderAware = Message.Datatype.BitField as IByteOrderAware;

        if (byteOrderAware is not null)
            DataUtils.CheckEndianness(byteOrderAware.ByteOrder);

        /* fast path for null dataspace */
        if (Message.Dataspace.Type == DataspaceType.Null)
            throw new Exception("Attributes with null dataspace cannot be read.");

        /* get decoder (succeeds only if decoding is possible) */
        var decoder = Message.Datatype.GetDecodeInfo<TElement>(
            context,
            /* isRawMode: not useful for attributes, but could be implemented later; 
             * note: compare to NativeDataset (look for endianness related code and #101) 
             */
            isRawMode: false);

        /* file element count */
        var fileElementCount = Message.Dataspace.GetTotalElementCount();

        return (decoder, fileElementCount);
    }

    #endregion
}