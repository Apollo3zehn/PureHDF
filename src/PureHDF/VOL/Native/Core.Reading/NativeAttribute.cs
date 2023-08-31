using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

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

    public T Read<T>()
    {
        // var byteOrderAware = Message.Datatype.BitField as IByteOrderAware;

        // if (byteOrderAware is not null)
        //     DataUtils.EnsureEndianness(source, destination.Span, byteOrderAware.ByteOrder, Message.Datatype.Size);

        var (elementType, _) = WriteUtils.GetElementType(typeof(T));

        // TODO cache this
        var method = _methodInfoReadCore.MakeGenericMethod(typeof(T), elementType);

        var source = new SystemMemoryStream(Message.InputData);

        var result = (T)method.Invoke(this, new object[] 
        {
            source 
        })!;

        return result;
    }

    private TResult ReadCore<TResult, TElement>(IH5ReadStream source)
    {
        // fast path for null dataspace
        if (Message.Dataspace.Type == DataspaceType.Null)
            throw new Exception("Attributes with null dataspace cannot be read.");

#warning ensure same element count for memory space and file space
        Array array = typeof(TResult).IsArray
            ? Array.CreateInstance(typeof(TElement), Message.Dataspace.Dimensions.Select(dim => (int)dim).ToArray())
            : new TResult[1];

        var targetElementCount = MathUtils.CalculateSize(Space.Dimensions, Message.Dataspace.Type);
        var targetBuffer = new ArrayMemoryManager<TElement>(array).Memory;

        var decoder = Message.Datatype.GetDecodeInfo<TElement>(_context);
        decoder(source, targetBuffer);

        return ReadUtils.FromArray<TResult, TElement>(array);
    }

    internal AttributeMessage Message { get; }

    internal DatatypeMessage InternalElementDataType { get; }

    #endregion
}