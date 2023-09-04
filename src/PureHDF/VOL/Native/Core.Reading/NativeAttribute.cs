using System.Reflection;

namespace PureHDF.VOL.Native;

internal class NativeAttribute : IH5Attribute
{
    #region Fields

    private static readonly MethodInfo _methodInfoReadCore = typeof(NativeAttribute)
        .GetMethod(nameof(ReadCore), BindingFlags.NonPublic | BindingFlags.Instance)!;

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

    public string Name => Message.Name;

    public IH5Dataspace Space
    {
        get
        {
            _space ??= new NativeDataspace(Message.Dataspace);

            return _space;
        }
    }

    public IH5DataType Type
    {
        get
        {
            _type ??= new NativeDataType(Message.Datatype);

            return _type;
        }
    }

    #endregion

    #region Methods

    public T Read<T>(ulong[]? memoryDims = null)
    {
        // var byteOrderAware = Message.Datatype.BitField as IByteOrderAware;

        // if (byteOrderAware is not null)
        //     DataUtils.EnsureEndianness(source, destination.Span, byteOrderAware.ByteOrder, Message.Datatype.Size);

        var (elementType, _) = WriteUtils.GetElementType(typeof(T));

        // TODO cache this
        var method = _methodInfoReadCore.MakeGenericMethod(typeof(T), elementType);

        var source = new SystemMemoryStream(Message.InputData);

        var result = (T)method.Invoke(this, new object?[] 
        {
            source,
            memoryDims
        })!;

        return result;
    }

    private TResult ReadCore<TResult, TElement>(
        IH5ReadStream source,
        ulong[]? memoryDims = null)
    {
        var resultType = typeof(TResult);
        var decoder = Message.Datatype.GetDecodeInfo<TElement>(_context);

        /* fast path for null dataspace */
        if (Message.Dataspace.Type == DataspaceType.Null)
            throw new Exception("Attributes with null dataspace cannot be read.");

        /* file element count */
        var fileElementCount = Message.Dataspace.GetTotalElementCount();

        /* memory dims */
        if (resultType.IsArray)
        {
            var rank = resultType.GetArrayRank();

            if (rank == 1)
                memoryDims ??= new ulong[] { fileElementCount };

            else if (rank == Message.Dataspace.Rank)
                memoryDims ??= Message.Dataspace.GetDims();

            else
                throw new Exception("The rank of the memory space must match the rank of the file space if no memory dimensions are provided.");
        }

        else
        {
            if (memoryDims == null)
                memoryDims = new ulong[] { 1 };
        }

        /* memory element count */
        var memoryElementCount = memoryDims.Aggregate(1UL, (product, dim) => product * dim);

        /* validation */
        if (memoryElementCount != fileElementCount)
            throw new Exception("The total file element count does not match the total memory element count.");

        /* target array */
        var array = resultType.IsArray
            ? Array.CreateInstance(typeof(TElement), memoryDims.Select(dim => (int)dim).ToArray())
            : new TResult[1];

        var resultBuffer = new ArrayMemoryManager<TElement>(array).Memory;

        /* decode */
        decoder(source, resultBuffer);

        /* convert & return */
        return ReadUtils.FromArray<TResult, TElement>(array);
    }

    internal AttributeMessage Message { get; }

    internal DatatypeMessage InternalElementDataType { get; }

    #endregion
}