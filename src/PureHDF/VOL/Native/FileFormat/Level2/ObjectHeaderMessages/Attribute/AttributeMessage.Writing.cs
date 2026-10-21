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

        var (elementType, isScalar) = WriteUtils.GetElementType(h5attribute.Type);

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
    ///         variable-length, since that is the one value a fixed-length field cannot represent, and so does one
    ///         whose measured width would not fit an object header message - see InternalCreate, which can only
    ///         test that once the datatype and dataspace exist. A fixed-length value is stored inline, so a wide
    ///         one competes with the rest of the message for the 65535 bytes it can declare, where a
    ///         variable-length value costs 16 bytes per element whatever its length.
    ///     </para>
    ///     <para>
    ///     The PADDING only governs the fixed-length path; a variable-length string ignores it and is always
    ///     declared <see cref="PaddingType.NullTerminate" /> by DatatypeMessage.GetTypeInfoForVariableLengthString.
    ///     Of the fixed-length cases, only MEASURE benefits from <see cref="PaddingType.NullPad" />: the width
    ///     is by construction filled to its last byte by the longest element, and NullTerminate would reserve
    ///     that byte for the terminator - so a tool rewriting the attribute through a wider datatype would
    ///     drop the last character of the very value the width was measured from. INHERIT keeps
    ///     <see cref="PaddingType.NullTerminate" /> to match PureHDF's pre-existing fixed-length behaviour,
    ///     so the backward-compat escape hatch changes nothing on disk. VARIABLELENGTH returns
    ///     <see langword="default" /> to signal the value is unused there (it falls through to
    ///     <see cref="PaddingType.NullTerminate" /> = 0, which the variable-length path overrides anyway).
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
        Memory<TElement> data
    )
    {
        if (typeof(TElement) != typeof(string))
            return (default, PaddingType.NullTerminate);

        return context.WriteOptions.AttributeStringLength switch
        {
            H5AttributeStringLength.Inherit => (default(int?), PaddingType.NullTerminate),
            H5AttributeStringLength.Measured => MeasureStringWidth(data),

            // An explicit zero rather than no answer at all, so that it overrides a DefaultStringLength that
            // declares a width - which is the only reason to ask for this.
            H5AttributeStringLength.VariableLength => (0, default),

            _ => throw new NotSupportedException(
                $"The attribute string length '{context.WriteOptions.AttributeStringLength}' is not supported."
            ),
        };

        static (int? Width, PaddingType Padding) MeasureStringWidth(Memory<TElement> data)
        {
            var span = data.Span;
            int width = 0;

            for (int i = 0; i < span.Length; i++)
            {
                // A fixed-length field has nowhere to keep the difference between null and empty, so a null
                // element hands the whole attribute back to variable-length rather than quietly becoming "".
                // Measuring is only worth doing where it loses nothing, which is the entire argument for it.
                if (span[i] is not string value)
                    return (0, default);

                width = Math.Max(width, Encoding.UTF8.GetByteCount(value));
            }

            // HDF5 has no zero-length string type, so an attribute holding only empty strings still needs
            // one byte.
            width = Math.Max(width, 1);

            return (width, PaddingType.NullPad);
        }
    }

    private static AttributeMessage InternalCreate<T, TElement>(
        NativeWriteContext context,
        string name,
        H5Attribute attribute,
        bool isScalar)
    {
        var memoryData = default(Memory<TElement>);
        var memoryDims = default(ulong[]);

        if (!attribute.IsNullDataspace)
        {
            (memoryData, memoryDims)
                = WriteUtils.ToMemory<T, TElement>(attribute.Data);
        }

        var type = memoryData.GetType();

        /* datatype */
        var (stringLength, stringPadding) = GetStringLengthForAttribute(context, memoryData);

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
        var fileDims = attribute.Dimensions ?? memoryDims;

        var dataspace = DataspaceMessage.Create(
            fileDims: fileDims);

        /* a measured width that does not fit hands the attribute back to variable-length */
        if (stringLength > 0 && GetEncodeSize(name, datatype, dataspace) > ushort.MaxValue)
        {
            (datatype, encode) = DatatypeMessage.Create(
                context,
                memoryData,
                isScalar,
                attribute.OpaqueInfo,
                0,
                default);
        }

        /* validation */
        if (dataspace.Type != DataspaceType.Null)
        {
            var fileTotalSize = dataspace.Dimensions
                .Aggregate(1UL, (x, y) => x * y);

            var memoryTotalSize = (memoryDims ?? throw new Exception("This should never happen."))
                .Aggregate(1UL, (x, y) => x * y);

            if (memoryDims.Any() && fileTotalSize != memoryTotalSize)
                throw new Exception("The actual number of elements does not match the total number of elements given in the dimensions parameter.");
        }

        // attribute
        // TODO avoid creation of system memory stream too often
        var dataEncodeSize = datatype.Size * dataspace.Dimensions
            .Aggregate(1UL, (product, dimension) => product * dimension);

        var buffer = new byte[dataEncodeSize];
        var localWriter = new SystemMemoryStream(buffer);

        var attributeMessage = new AttributeMessage(
            Flags: AttributeMessageFlags.None,
            Name: name,
            Datatype: datatype,
            Dataspace: dataspace,
            InputData: default,
            EncodeData: driver =>
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

    /// <summary>
    /// The exact encoded size of the attribute message, in a width that cannot wrap.
    /// </summary>
    /// <remarks>
    /// A header message declares its own size in two bytes (HeaderMessage.Encode), so
    /// <see cref="ushort.MaxValue" /> is the most an attribute message can be. The data is only part of
    /// that budget: the name, the datatype and the dataspace are counted in the same two bytes, so the
    /// limit has to be tested against the whole message and not against the data alone.
    /// <para>
    /// ObjectHeader2.Encode allocates the object header from these sizes and then writes the messages at
    /// their real length, so a size that wraps is not a wrong number in isolation - the header overruns
    /// its own allocation, and the object it belongs to stops being readable at all.
    /// </para>
    /// </remarks>
    internal static ulong GetEncodeSize(
        string name,
        DatatypeMessage datatype,
        DataspaceMessage dataspace)
    {
        var nameEncodeSize = (ulong)Encoding.UTF8.GetByteCount(name) + 1;

        var dataSize = datatype.Size * dataspace.Dimensions
            .Aggregate(1UL, (product, dimension) => product * dimension);

        return
            sizeof(byte) +
            sizeof(byte) +
            sizeof(ushort) +
            sizeof(ushort) +
            sizeof(ushort) +
            sizeof(byte) +
            nameEncodeSize +
            datatype.GetEncodeSize() +
            dataspace.GetEncodeSize() +
            dataSize;
    }

    public override ushort GetEncodeSize()
    {
        if (Version != 3)
            throw new Exception("Only version 3 attribute messages are supported.");

        var size = GetEncodeSize(Name, Datatype, Dataspace);

        if (size > ushort.MaxValue)
            throw new Exception(
                $"The attribute '{Name}' encodes to {size} bytes, which is more than the "
                + $"{ushort.MaxValue} bytes an object header message can declare. Store the value as a "
                + "dataset instead.");

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
        var nameBytes = Encoding.UTF8.GetBytes(Name);
        driver.Write((ushort)(nameBytes.Length + 1));

        // datatype size
        var dataTypeEncodeSize = Datatype.GetEncodeSize();
        driver.Write(dataTypeEncodeSize);

        // dataspace size
        var dataSpaceEncodeSize = Dataspace.GetEncodeSize();
        driver.Write(dataSpaceEncodeSize);

        // name character set encoding
        if (Version == 3)
            driver.Write((byte)CharacterSetEncoding.UTF8);

        // name
        if (Version == 1)
        {
            throw new NotImplementedException() /* Version 1 requires padding */;
        }

        else
        {
            driver.Write(nameBytes);
            driver.Write((byte)0);
        }

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