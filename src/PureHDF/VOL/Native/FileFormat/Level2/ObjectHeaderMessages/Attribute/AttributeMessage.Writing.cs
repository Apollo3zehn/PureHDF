using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text;

namespace PureHDF.VOL.Native;

internal partial record class AttributeMessage
{
    private static readonly MethodInfo _methodInfoCreateAttributeMessage = typeof(AttributeMessage)
        .GetMethod(nameof(InternalCreate), BindingFlags.NonPublic | BindingFlags.Static)!;

    public static AttributeMessage Create(
        NativeWriteContext context,
        string name,
        object attribute)
    {
        if (attribute is not H5Attribute h5attribute)
            h5attribute = new H5Attribute(attribute);

        (var elementType, bool isScalar) = WriteUtils.GetElementType(h5attribute.Type);

        // TODO cache this
        var method = _methodInfoCreateAttributeMessage.MakeGenericMethod(h5attribute.Type, elementType);

        try
        {
            return (AttributeMessage)method.Invoke(default, [context, name, h5attribute, isScalar])!;
        }

        // Rethrown unwrapped, as in H5NativeWriter.EncodeDataset and for the same reason: this is where a
        // caller's own data is rejected, and reflection would otherwise replace a specific message with
        // "Exception has been thrown by the target of an invocation".
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            throw; // unreachable; the compiler cannot see that the line above always throws
        }
    }

    /// <summary>
    ///     The width to declare for a string attribute, and the padding that width implies. A null width means
    ///     no answer, deferring to <see cref="H5WriteOptions.DefaultStringLength" />.
    /// </summary>
    /// <remarks>
    ///     <see cref="H5WriteOptions.AttributeStringLength" /> decides, independently of
    ///     <see cref="H5WriteOptions.DefaultStringLength" />. The default is to MEASURE, because that option is
    ///     FILE-GLOBAL: one width has to serve the widest attribute anywhere in the file, so every narrower
    ///     attribute pays the difference in padding, and any value that exceeds it is truncated - by byte, so
    ///     mid-UTF-8-sequence. A width taken from the value pads nothing and cannot truncate it.
    ///     <para>
    ///         A measured width is only taken where it costs nothing: an attribute holding a null element stays
    ///         variable-length, since that is the one value a fixed-length field cannot represent.
    ///     </para>
    ///     <para>
    ///         The width is always declared <see cref="PaddingType.NullPad" />. A measured width is by
    ///         construction filled to its last byte by the longest element, and
    ///         DatatypeMessage.GetTypeInfoForFixedLengthString has what NullTerminate costs such a value.
    ///     </para>
    ///     <para>
    ///         Reaches an attribute whose ELEMENTS are strings. A string that is a MEMBER of a compound attribute
    ///         is sized by <see cref="H5WriteOptions.DefaultStringLength" /> or a string length mapper whatever the
    ///         setting says, for the same reason datasets are left out entirely: a member's width has to cover
    ///         every row and stay uniform across the objects sharing that type, so it must not be derived from one
    ///         value.
    ///     </para>
    /// </remarks>
    private static (int? Width, PaddingType Padding) GetStringLengthForAttribute<TElement>(
        NativeWriteContext context,
        Memory<TElement> data)
    {
        if (typeof(TElement) != typeof(string))
            return (default, PaddingType.NullTerminate);

        var mode = context.WriteOptions.AttributeStringLength;

        if (mode == H5AttributeStringLength.Inherit)
            return (default, PaddingType.NullTerminate);

        // An explicit zero rather than no answer at all, so that it overrides a DefaultStringLength that
        // declares a width - which is the only reason to ask for this.
        if (mode == H5AttributeStringLength.VariableLength)
            return (0, PaddingType.NullTerminate);

        if (mode != H5AttributeStringLength.Measured)
            throw new NotSupportedException($"The attribute string length '{mode}' is not supported.");

        var span = data.Span;
        int width = 0;

        for (int i = 0; i < span.Length; i++)
        {
            // A fixed-length field has nowhere to keep the difference between null and empty, so a null
            // element hands the whole attribute back to variable-length rather than quietly becoming "".
            // Measuring is only worth doing where it loses nothing, which is the entire argument for it.
            if (span[i] is not string value)
                return (0, PaddingType.NullTerminate);

            width = Math.Max(width, Encoding.UTF8.GetByteCount(value));
        }

        // HDF5 has no zero-length string type, so an attribute holding only empty strings still needs one
        // byte.
        width = Math.Max(width, 1);

        return (width, PaddingType.NullPad);
    }

    private static AttributeMessage InternalCreate<T, TElement>(
        NativeWriteContext context,
        string name,
        H5Attribute attribute,
        bool isScalar)
    {
        var memoryData = default(Memory<TElement>);
        ulong[]? memoryDims = default;

        if (!attribute.IsNullDataspace)
            (memoryData, memoryDims)
                = WriteUtils.ToMemory<T, TElement>(attribute.Data);

        var type = memoryData.GetType();

        /* datatype */
        (int? stringLength, var stringPadding) = GetStringLengthForAttribute(context, memoryData);

        var (datatype, encode) =
            DatatypeMessage.Create(
                context,
                memoryData,
                isScalar,
                attribute.OpaqueInfo,
                stringLength,
                stringPadding);

        if (attribute.OpaqueInfo is not null && datatype.Class == DatatypeMessageClass.Opaque)
            memoryDims = [(ulong)memoryData.Length / attribute.OpaqueInfo.TypeSize];

        /* dataspace */
        ulong[]? fileDims = attribute.Dimensions ?? memoryDims;

        var dataspace = DataspaceMessage.Create(
            fileDims);

        /* validation */
        if (dataspace.Type != DataspaceType.Null)
        {
            ulong fileTotalSize = dataspace.Dimensions
                .Aggregate(1UL, (x, y) => x * y);

            ulong memoryTotalSize = (memoryDims ?? throw new Exception("This should never happen."))
                .Aggregate(1UL, (x, y) => x * y);

            if (memoryDims.Any() && fileTotalSize != memoryTotalSize)
                throw new Exception(
                    "The actual number of elements does not match the total number of elements given in the dimensions parameter.");
        }

        // attribute
        // TODO avoid creation of system memory stream too often
        ulong dataEncodeSize = datatype.Size * dataspace.Dimensions
            .Aggregate(1UL, (product, dimension) => product * dimension);

        byte[] buffer = new byte[dataEncodeSize];
        var localWriter = new SystemMemoryStream(buffer);

        var attributeMessage = new AttributeMessage(
            AttributeMessageFlags.None,
            name,
            datatype,
            dataspace,
            default,
            driver =>
            {
                encode(memoryData, localWriter);
                driver.Write(buffer);
            }
        )
        {
            Version = 3
        };

        return attributeMessage;
    }

    public override ushort GetEncodeSize()
    {
        if (Version != 3)
            throw new Exception("Only version 3 attribute messages are supported.");

        int nameEncodeSize = Encoding.UTF8.GetBytes(Name).Length + 1;
        ulong dataSize = Datatype.Size *
                         Dataspace.Dimensions.Aggregate(1UL, (product, dimension) => product * dimension);

        // TODO: make this more exact?
        if (dataSize > 64 * 1024)
            throw new Exception("The maximum attribute size is 64KB.");

        int size =
            sizeof(byte) +
            sizeof(byte) +
            sizeof(ushort) +
            sizeof(ushort) +
            sizeof(ushort) +
            sizeof(byte) +
            nameEncodeSize +
            Datatype.GetEncodeSize() +
            Dataspace.GetEncodeSize() +
            (ushort)dataSize;

        return (ushort)size;
    }

    public override void Encode(H5DriverBase driver)
    {
        // version
        driver.Write(Version);

        // flags
        if (Version == 1)
            driver.Seek(1, SeekOrigin.Current);

        else
            driver.Write((byte)Flags);

        // name size
        byte[] nameBytes = Encoding.UTF8.GetBytes(Name);
        driver.Write((ushort)(nameBytes.Length + 1));

        // datatype size
        ushort dataTypeEncodeSize = Datatype.GetEncodeSize();
        driver.Write(dataTypeEncodeSize);

        // dataspace size
        ushort dataSpaceEncodeSize = Dataspace.GetEncodeSize();
        driver.Write(dataSpaceEncodeSize);

        // name character set encoding
        if (Version == 3)
            driver.Write((byte)CharacterSetEncoding.UTF8);

        // name
        if (Version == 1) throw new NotImplementedException() /* Version 1 requires padding */;

        driver.Write(nameBytes);
        driver.Write((byte)0);

        // datatype
        Datatype.Encode(driver);

        if (Version == 1)
            throw new NotImplementedException() /* Version 1 requires padding */;

        // dataspace
        Dataspace.Encode(driver);

        if (Version == 1)
            throw new NotImplementedException() /* Version 1 requires padding */;

        // data
        EncodeData.Invoke(driver);
    }
}