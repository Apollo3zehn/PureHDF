using System.Reflection;

namespace PureHDF.VOL.Native;

/// <summary>
/// A native HDF5 attribute.
/// </summary>
public class NativeAttribute : IH5Attribute
{
    #region Fields

    private static readonly MethodInfo _methodInfoReadCoreLevel1 = typeof(NativeAttribute)
        .GetMethod(nameof(ReadCoreLevel1_generic), BindingFlags.NonPublic | BindingFlags.Instance)!;

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

        // TODO cache this
        var method = _methodInfoReadCoreLevel1.MakeGenericMethod(typeof(T), elementType);
        var source = new SystemMemoryStream(Message.InputData);

        var result = (T)method.Invoke(this,
        [
            default /* buffer */,
            source,
            memoryDims
        ])!;

        return result;
    }

    /// <inheritdoc />
    public void Read<T>(
        T buffer,
        ulong[]? memoryDims = null)
    {
        var (elementType, _) = WriteUtils.GetElementType(typeof(T));

        // TODO cache this
        var method = _methodInfoReadCoreLevel1.MakeGenericMethod(typeof(T), elementType);
        var source = new SystemMemoryStream(Message.InputData);

        method.Invoke(this,
        [
            buffer,
            source,
            memoryDims
        ]);
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

    private TResult? ReadCoreLevel1_generic<TResult, TElement>(
        TResult? buffer,
        IH5ReadStream source,
        ulong[]? memoryDims = null)
    {
        var (decoder, fileElementCount) = GetDecoderAndFileElementCount<TElement>();

        /* result buffer / result array */
        Span<TElement> resultBuffer;
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

                else
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

            resultBuffer = new ArrayMemoryManager<TElement>(resultArray).Memory.Span;
        }

        else
        {
            /* result buffer */
            (var resultMemoryBuffer, memoryDims) = ReadUtils.ToMemory<TResult, TElement>(buffer);
            resultBuffer = resultMemoryBuffer.Span;
        }

        ReadCoreLevel2(source, memoryDims, fileElementCount, decoder, resultBuffer);

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
        var (decoder, fileElementCount) = GetDecoderAndFileElementCount<TElement>();

        /* result buffer */
        if (memoryDims is null)
            memoryDims = [(ulong)buffer.Length];

        var resultBuffer = buffer;

        ReadCoreLevel2(source, memoryDims, fileElementCount, decoder, resultBuffer);
    }

    private static void ReadCoreLevel2<TElement>(
        IH5ReadStream source,
        ulong[] memoryDims,
        ulong fileElementCount,
        DecodeDelegate<TElement> decoder,
        Span<TElement> resultBuffer)
    {
        /* memory element count */
        var memoryElementCount = memoryDims.Aggregate(1UL, (product, dim) => product * dim);

        /* validation */
        if (memoryElementCount != fileElementCount)
            throw new Exception("The total file element count does not match the total memory element count.");

        /* decode */
        decoder(source, resultBuffer);
    }

    private (DecodeDelegate<TElement>, ulong) GetDecoderAndFileElementCount<TElement>()
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
            _context, 
            isRawMode: false /* not useful for attributes, but could be implemented later */);

        /* file element count */
        var fileElementCount = Message.Dataspace.GetTotalElementCount();

        return (decoder, fileElementCount);
    }

    #endregion
}