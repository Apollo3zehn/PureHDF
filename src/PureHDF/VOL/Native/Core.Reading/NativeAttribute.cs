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

        var targetElementCount = MathUtils.CalculateSize(Space.Dimensions, Message.Dataspace.Type);

#if NET6_0_OR_GREATER

        var array = Array.CreateInstance(
            typeof(TElement), 
            Space.Dimensions.Select(dim => (int)dim).ToArray());

        var span = MemoryMarshal.CreateSpan(
            reference: ref MemoryMarshal.GetArrayDataReference(array), 
            length: array.Length * Unsafe.SizeOf<TElement>());

        var b2 = Unsafe.As<TElement[]>(array);

        var c = 1;

#endif

        throw new NotImplementedException();

        // var targetBuffer = new UnmanagedArrayMemoryManager<TElement>(array).Memory;

        // var decoder = Message.Datatype.GetDecodeInfo<TElement>(_context);
        // decoder(source, targetBuffer);

        // return ReadUtils.FromArray<TResult, TElement>(targetBuffer);
    }

    internal AttributeMessage Message { get; }

    internal DatatypeMessage InternalElementDataType { get; }

    #endregion
}