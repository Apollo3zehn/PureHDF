using System.Buffers;
using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace PureHDF.VOL.Native;

// TODO: use this for generic structs
// - https://github.com/SergeyTeplyakov/ObjectLayoutInspector?
// - https://stackoverflow.com/a/56512720

internal partial record class DatatypeMessage : Message
{
    private const int DATATYPE_MESSAGE_VERSION = 3;

    // reference size                = GHEAP address + GHEAP index
    private const int REFERENCE_SIZE = sizeof(ulong) + sizeof(uint);

    // variable length entry size           length
    private const int VLEN_REFERENCE_SIZE = sizeof(uint) + REFERENCE_SIZE;

    private static readonly MethodInfo _methodInfoGetTypeInfoForTopLevelUnmanagedMemory = typeof(DatatypeMessage)
        .GetMethod(nameof(GetTypeInfoForTopLevelUnmanagedMemory), BindingFlags.NonPublic | BindingFlags.Static)!;

    // stringLength declares the width for a fixed-length string, overriding
    // H5WriteOptions.DefaultStringLength. Null defers to that option, which is what every dataset does;
    // an attribute answers for itself according to H5WriteOptions.AttributeStringLength - a measured width
    // along with the padding it implies, or an explicit 0 to force variable-length even where the option
    // declares a width. See AttributeMessage.GetStringLengthForAttribute.
    public static (DatatypeMessage, EncodeDelegate<T>) Create<T>(
        NativeWriteContext context,
        Memory<T> topLevelData,
        bool isScalar,
        H5OpaqueInfo? opaqueInfo,
        int? stringLength = default,
        PaddingType stringPadding = PaddingType.NullTerminate
    )
    {
        bool isScalarDictionary = isScalar &&
                                  !topLevelData.Equals(default) &&
                                  typeof(IDictionary).IsAssignableFrom(typeof(T)) &&
                                  typeof(T).GenericTypeArguments[0] == typeof(string);

        if (isScalar)
            return isScalarDictionary
                ? GetTypeInfoForTopLevelDictionary<T>(context, (IDictionary)topLevelData.Span[0]!)
                : GetTypeInfoForScalar_SpecialEncode<T>(context, stringLength, stringPadding);

        return
            DataUtils.IsReferenceOrContainsReferences(typeof(T)) ||
            Nullable.GetUnderlyingType(typeof(T)) is not null
                ? GetTypeInfoForTopLevelMemory<T>(context, opaqueInfo, stringLength, stringPadding)
                : ((DatatypeMessage, EncodeDelegate<T>))_methodInfoGetTypeInfoForTopLevelUnmanagedMemory
                    // TODO cache
                    .MakeGenericMethod(typeof(T))
                    .Invoke(default, [context, opaqueInfo])!;
    }

    public override void Encode(H5DriverBase driver)
    {
        byte classVersion = (byte)(((byte)Class & 0x0F) | (Version << 4));
        driver.Write(classVersion);

        BitField.Encode(driver);

        driver.Write(Size);

        foreach (var property in Properties) property.Encode(driver, Size);
    }

    public override ushort GetEncodeSize()
    {
        int propertiesEncodeSize = Properties.Aggregate(0, (sum, properties)
            => sum + properties.GetEncodeSize(Size));

        int encodeSize =
            sizeof(byte) +
            sizeof(byte) * 3 +
            sizeof(uint) +
            propertiesEncodeSize;

        return (ushort)encodeSize;
    }

    private static (DatatypeMessage, EncodeDelegate<T>) GetTypeInfoForScalar_SpecialEncode<T>(
        NativeWriteContext context,
        int? stringLength = default,
        PaddingType stringPadding = PaddingType.NullTerminate)
    {
        var (dataType, encode) = GetTypeInfoForScalar(context, typeof(T), stringLength, stringPadding: stringPadding);

        void encodeFirstElement(Memory<T> source, IH5WriteStream target)
        {
            encode(source.Span[0]!, target);
        }

        return (dataType, encodeFirstElement);
    }

    private static (DatatypeMessage, ElementEncodeDelegate) GetTypeInfoForScalar(
        NativeWriteContext context,
        Type type,
        int? stringLength = default,
        H5OpaqueInfo? opaqueInfo = default,
        PaddingType stringPadding = PaddingType.NullTerminate)
    {
        // Null means no answer was given, so the file-global option decides. A zero IS an answer - it asks
        // for a variable-length string even where that option declares a width.
        int resolvedStringLength = stringLength ?? context.WriteOptions.DefaultStringLength;

        // special case: opaque (= byte[])
        // use unique type to make cache happy
        if (type == typeof(byte) && opaqueInfo is not null)
            type = typeof(H5OpaqueInfo);

        // Cache. The key is the type plus everything else the message depends on — a string's
        // requested width, an opaque type's size and tag — because those come from the caller,
        // so one type maps to many messages. Keying on all of it means strings and opaque types
        // can be cached like anything else instead of having to be excluded.
        var cache = context.TypeToMessageMap;
        var cacheKey = new DatatypeCacheKey(type, resolvedStringLength, stringPadding, opaqueInfo);

        if (cache.TryGetValue(cacheKey, out var cachedMessage))
            return cachedMessage;

        //
        var endianness = BitConverter.IsLittleEndian
            ? ByteOrder.LittleEndian
            : ByteOrder.BigEndian;

        var (newMessage, encode) = type switch
        {
            /* string */
            Type when type == typeof(string)
                => resolvedStringLength == 0
                    ? GetTypeInfoForVariableLengthString(context)
                    : GetTypeInfoForFixedLengthString(context, resolvedStringLength, stringPadding),

            /* dictionary */
            Type when typeof(IDictionary).IsAssignableFrom(type) &&
                      type.GenericTypeArguments[0] == typeof(string)
                => GetTypeInfoForVariableLengthSequence(context, typeof(KeyValuePair<,>)
                    .MakeGenericType(type.GenericTypeArguments)),

            /* array */
            Type when DataUtils.IsArray(type)
                /* there is no multi-dim variable-length sequence in HDF5 */
                => GetTypeInfoForVariableLengthSequence(context, type.GetElementType()!),

            /* generic IEnumerable */
            Type when typeof(IEnumerable).IsAssignableFrom(type) && type.IsGenericType
                => GetTypeInfoForVariableLengthSequence(context, type.GenericTypeArguments[0]),

            /* object reference */
            Type when type == typeof(H5ObjectReference)
                => GetTypeInfoForObjectReference(context),

            /* opaque */
            Type when type == typeof(H5OpaqueInfo)
                => GetTypeInfoForOpaque(opaqueInfo!),

            /* remaining reference types */
            Type when DataUtils.IsReferenceOrContainsReferences(type)
                => GetTypeInfoForReferenceLikeType(context, type),

            /* non blittable (but unmanged!) */
            /* https://stackoverflow.com/questions/65833341/does-c-sharp-enforce-that-an-unmanaged-type-is-blittable#comment116401977_65833341 */
            Type when type == typeof(bool)
                => GetTypeInfoForBool(context),

            /* enumeration */
            Type when type.IsEnum
                => GetTypeInfoForEnum(context, type),

            /* unsigned fixed-point types */
            Type when
                type == typeof(byte) ||
                type == typeof(ushort) ||
                type == typeof(uint) ||
                type == typeof(ulong)
#if NET8_0_OR_GREATER
                || type == typeof(UInt128)
#endif
                => GetTypeInfoForUnsignedFixedPointTypes(type, endianness),

            /* signed fixed-point types */
            Type when
                type == typeof(sbyte) ||
                type == typeof(short) ||
                type == typeof(int) ||
                type == typeof(long)
#if NET8_0_OR_GREATER
                || type == typeof(Int128)
#endif
                => GetTypeInfoForSignedFixedPointTypes(type, endianness),

            /* 16 bit floating-point */
            Type when type == typeof(Half)
                => GetTypeInfoFor16BitFloatingPoint(type, endianness),

            /* 32 bit floating-point */
            Type when type == typeof(float)
                => GetTypeInfoFor32BitFloatingPoint(type, endianness),

            /* 64 bit floating-point */
            Type when type == typeof(double)
                => GetTypeInfoFor64BitFloatingPoint(type, endianness),

            /* Nullable<ValueType> */
            Type when Nullable.GetUnderlyingType(type) is not null
                => GetTypeInfoForNullableValueType(context, type, Nullable.GetUnderlyingType(type)!),

            /* remaining non-generic value types */
            Type when type.IsValueType && !type.IsGenericType
                => context.WriteOptions.IncludeStructProperties
                    ? GetTypeInfoForReferenceLikeType(context, type)
                    : GetTypeInfoForValueType(context, type),

            /* remaining generic value types */
            Type when type.IsValueType
                => GetTypeInfoForReferenceLikeType(context, type),

            _ => throw new NotSupportedException($"The data type '{type}' is not supported.")
        };

        cache[cacheKey] = (newMessage, encode);

        return (newMessage, encode);
    }

    private static (DatatypeMessage, ElementEncodeDelegate) GetTypeInfoForBool(
        NativeWriteContext context)
    {
        var (baseMessage, _) = GetTypeInfoForScalar(context, typeof(byte));

        static void encode(object source, IH5WriteStream target)
        {
            Span<byte> buffer = stackalloc byte[]
            {
                (bool)source ? (byte)1 : (byte)0
            };

            target.WriteDataset(buffer);
        }

        return (baseMessage, encode);
    }

    private static (DatatypeMessage, ElementEncodeDelegate) GetTypeInfoForEnum(
        NativeWriteContext context,
        Type type)
    {
        var underlyingType = Enum.GetUnderlyingType(type);
        var enumValues = Enum.GetValues(type);
        object[] enumObjects = new object[enumValues.Length];

        for (int i = 0; i < enumValues.Length; i++) enumObjects[i] = enumValues.GetValue(i)!;

        byte[][] values = (underlyingType switch
        {
            Type t when t == typeof(byte) => enumObjects.Select(enumValue => new[] { (byte)enumValue }),
            Type t when t == typeof(sbyte) => enumObjects.Select(enumValue => new[] { unchecked((byte)enumValue) }),
            Type t when t == typeof(ushort) =>
                enumObjects.Select(enumValue => BitConverter.GetBytes((ushort)enumValue)),
            Type t when t == typeof(short) => enumObjects.Select(enumValue => BitConverter.GetBytes((short)enumValue)),
            Type t when t == typeof(uint) => enumObjects.Select(enumValue => BitConverter.GetBytes((uint)enumValue)),
            Type t when t == typeof(int) => enumObjects.Select(enumValue => BitConverter.GetBytes((int)enumValue)),
            Type t when t == typeof(ulong) => enumObjects.Select(enumValue => BitConverter.GetBytes((ulong)enumValue)),
            Type t when t == typeof(long) => enumObjects.Select(enumValue => BitConverter.GetBytes((long)enumValue)),
            _ => throw new Exception($"The enum type {underlyingType} is not supported.")
        }).ToArray();

        var (baseMessage, baseEncode) = GetTypeInfoForScalar(context, Enum.GetUnderlyingType(type));

        var properties = new EnumerationPropertyDescription(
            baseMessage,
            Enum.GetNames(type),
            values
        );

        var message = new DatatypeMessage(
            baseMessage.Size,
            new EnumerationBitFieldDescription(
                (ushort)Enum.GetNames(type).Length
            ),
            [
                properties
            ]
        )
        {
            Version = DATATYPE_MESSAGE_VERSION,
            Class = DatatypeMessageClass.Enumerated
        };

        return (message, baseEncode);
    }

    private static (DatatypeMessage, ElementEncodeDelegate) GetTypeInfoForValueType(
        NativeWriteContext context,
        Type type)
    {
        var fieldInfos = type.GetFields(BindingFlags.Public | BindingFlags.Instance);
        var properties = new CompoundPropertyDescription[fieldInfos.Length];

        for (int i = 0; i < fieldInfos.Length; i++)
        {
            var fieldInfo = fieldInfos[i];
            var underlyingType = fieldInfo.FieldType;
            var (fieldMessage, _) = GetTypeInfoForScalar(context, underlyingType);
            var fieldNameMapper = context.WriteOptions.FieldNameMapper;

            properties[i] = new CompoundPropertyDescription(
                fieldNameMapper is null ? fieldInfo.Name : fieldNameMapper(fieldInfo) ?? fieldInfo.Name,
                (ulong)Marshal.OffsetOf(type, fieldInfo.Name),
                fieldMessage
            );
        }

        var bitfield = new CompoundBitFieldDescription(
            (ushort)fieldInfos.Length
        );

        /* H5Odtype.c (H5O_dtype_decode_helper: case H5T_COMPOUND) */
        if (bitfield.MemberCount == 0)
            throw new Exception("The compound data type needs at least one member");

        var message = new DatatypeMessage(
            (uint)Marshal.SizeOf(type),
            bitfield,
            properties
        )
        {
            Version = DATATYPE_MESSAGE_VERSION,
            Class = DatatypeMessageClass.Compound
        };

        var invokeEncodeUnmanagedElement = WriteUtils.MethodInfoEncodeUnmanagedElement.MakeGenericMethod(type);
        object[] parameters = new object[2];

        void encode(object source, IH5WriteStream target)
        {
            parameters[0] = source;
            parameters[1] = target;
            invokeEncodeUnmanagedElement.Invoke(default, parameters);
        }

        ;

        return (message, encode);
    }

    private static (DatatypeMessage, ElementEncodeDelegate) GetTypeInfoForReferenceLikeType(
        NativeWriteContext context,
        Type type)
    {
        CompoundBitFieldDescription bitfield;

        uint offset = 0U;
        bool isValueType = type.IsValueType;
        int defaultStringLength = context.WriteOptions.DefaultStringLength;

        // fields
        bool includeFields = isValueType
            ? context.WriteOptions.IncludeStructFields
            : context.WriteOptions.IncludeClassFields;

        var fieldInfos = includeFields
            ? type.GetFields(BindingFlags.Public | BindingFlags.Instance)
            : Array.Empty<FieldInfo>();

        var fieldEncodes = includeFields
            ? new ElementEncodeDelegate[fieldInfos.Length]
            : Array.Empty<ElementEncodeDelegate>();

        // properties
        bool includeProperties = isValueType
            ? context.WriteOptions.IncludeStructProperties
            : context.WriteOptions.IncludeClassProperties;

        var propertyInfos = includeProperties
            ? type
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(propertyInfo => propertyInfo.CanRead)
                .ToArray()
            : Array.Empty<PropertyInfo>();

        var propertyEncodes = includeProperties
            ? new ElementEncodeDelegate[propertyInfos.Length]
            : Array.Empty<ElementEncodeDelegate>();

        // bitfield
        bitfield = new CompoundBitFieldDescription(
            (ushort)(fieldInfos.Length + propertyInfos.Length)
        );

        /* H5Odtype.c (H5O_dtype_decode_helper: case H5T_COMPOUND) */
        if (bitfield.MemberCount == 0)
            throw new Exception("The compound data type needs at least one member.");

        // propertyDescriptions
        var properties = new CompoundPropertyDescription[bitfield.MemberCount];

        if (includeFields)
        {
            var fieldNameMapper = context.WriteOptions.FieldNameMapper;
            var fieldStringLengthMapper = context.WriteOptions.FieldStringLengthMapper;

            for (int i = 0; i < fieldInfos.Length; i++)
            {
                var fieldInfo = fieldInfos[i];
                var underlyingType = fieldInfo.FieldType;

                int stringLength = underlyingType == typeof(string)
                    ? fieldStringLengthMapper is null
                        ? defaultStringLength
                        : fieldStringLengthMapper(fieldInfo) ?? defaultStringLength
                    : defaultStringLength;

                var (fieldMessage, fieldEncode) = GetTypeInfoForScalar(context, underlyingType, stringLength);

                fieldEncodes[i] = fieldEncode;

                properties[i] = new CompoundPropertyDescription(
                    fieldNameMapper is null ? fieldInfo.Name : fieldNameMapper(fieldInfo) ?? fieldInfo.Name,
                    offset,
                    fieldMessage
                );

                offset += fieldMessage.Size;
            }
        }

        if (includeProperties)
        {
            var propertyNameMapper = context.WriteOptions.PropertyNameMapper;
            var propertyStringLengthMapper = context.WriteOptions.PropertyStringLengthMapper;

            for (int i = 0; i < propertyInfos.Length; i++)
            {
                var propertyInfo = propertyInfos[i];
                var underlyingType = propertyInfo.PropertyType;

                int stringLength = underlyingType == typeof(string)
                    ? propertyStringLengthMapper is null
                        ? defaultStringLength
                        : propertyStringLengthMapper(propertyInfo) ?? defaultStringLength
                    : defaultStringLength;

                var (propertyMessage, propertyEncode) = GetTypeInfoForScalar(context, underlyingType, stringLength);

                propertyEncodes[i] = propertyEncode;

                properties[fieldInfos.Length + i] = new CompoundPropertyDescription(
                    propertyNameMapper is null
                        ? propertyInfo.Name
                        : propertyNameMapper(propertyInfo) ?? propertyInfo.Name,
                    offset,
                    propertyMessage
                );

                offset += propertyMessage.Size;
            }
        }

        void encode(object source, IH5WriteStream target)
        {
            // fields
            for (int i = 0; i < fieldEncodes.Length; i++)
            {
                var memberEncode = fieldEncodes[i];
                int typeSize = (int)properties[i].MemberTypeMessage.Size;
                var fieldInfo = fieldInfos[i];

                memberEncode(fieldInfo.GetValue(source)!, target);
            }

            // properties
            for (int i = 0; i < propertyEncodes.Length; i++)
            {
                var memberEncode = propertyEncodes[i];
                int typeSize = (int)properties[i].MemberTypeMessage.Size;
                var propertyInfo = propertyInfos[i];

                memberEncode(propertyInfo.GetValue(source)!, target);
            }
        }

        var message = new DatatypeMessage(
            offset,
            bitfield,
            properties
        )
        {
            Version = DATATYPE_MESSAGE_VERSION,
            Class = DatatypeMessageClass.Compound
        };

        return (message, encode);
    }

    private static (DatatypeMessage, ElementEncodeDelegate) GetTypeInfoForNullableValueType(
        NativeWriteContext context,
        Type type,
        Type baseType)
    {
        var (baseMessage, baseEncode) = GetTypeInfoForScalar(context, baseType);

        var message = new DatatypeMessage(
            VLEN_REFERENCE_SIZE,
            new VariableLengthBitFieldDescription(
                InternalVariableLengthType.Sequence,
                default,
                default
            ),
            [
                new VariableLengthPropertyDescription(
                    baseMessage
                )
            ]
        )
        {
            Version = DATATYPE_MESSAGE_VERSION,
            Class = DatatypeMessageClass.VariableLength
        };

        void encode(object source, IH5WriteStream target)
        {
            var globalHeapId = default(WritingGlobalHeapId);
            Span<int> lengthArray = stackalloc int[] { 1 };

            if (source is not null)
            {
                int itemCount = 1;

                uint typeSize = ((VariableLengthPropertyDescription)message.Properties[0])
                    .BaseType
                    .Size;

                int totalLength = (int)typeSize * itemCount;

                (globalHeapId, var memory) = context.GlobalHeapManager
                    .AddObject(totalLength);

                /* Cannot use context.ShortlivedStream here because baseEncode could recursively call
                 * this method and then the ShortlivedStream would be reset too early.
                 */
                var localTarget = new SystemMemoryStream(memory);

                // encode item
                baseEncode(source, localTarget);
            }

            // encode variable length object
            target.WriteDataset(MemoryMarshal.AsBytes(lengthArray));

            Span<WritingGlobalHeapId> gheapIdArray
                = stackalloc WritingGlobalHeapId[] { globalHeapId };

            target.WriteDataset(MemoryMarshal.AsBytes(gheapIdArray));
        }

        return (message, encode);
    }

    private static (DatatypeMessage, ElementEncodeDelegate) GetTypeInfoForVariableLengthSequence(
        NativeWriteContext context,
        Type baseType)
    {
        var (baseMessage, baseEncode) = GetTypeInfoForScalar(context, baseType);

        var message = new DatatypeMessage(
            VLEN_REFERENCE_SIZE,
            new VariableLengthBitFieldDescription(
                InternalVariableLengthType.Sequence,
                default,
                default
            ),
            [
                new VariableLengthPropertyDescription(
                    baseMessage
                )
            ]
        )
        {
            Version = DATATYPE_MESSAGE_VERSION,
            Class = DatatypeMessageClass.VariableLength
        };

        void encode(object source, IH5WriteStream target)
        {
            var globalHeapId = default(WritingGlobalHeapId);
            Span<int> lengthArray = stackalloc int[1];

            if (source is not null)
            {
                var enumerable = (IEnumerable)source;
                int itemCount = WriteUtils.GetEnumerableLength(enumerable);

                uint typeSize = ((VariableLengthPropertyDescription)message.Properties[0])
                    .BaseType
                    .Size;

                int totalLength = (int)typeSize * itemCount;
                lengthArray[0] = itemCount;

                (globalHeapId, var memory) = context.GlobalHeapManager
                    .AddObject(totalLength);

                /* Cannot use context.ShortlivedStream here because baseEncode could recursively call
                 * this method and then the ShortlivedStream would be reset too early.
                 */
                var localTarget = new SystemMemoryStream(memory);

                // encode items
                foreach (object? item in enumerable) baseEncode(item, localTarget);
            }

            // encode variable length object
            target.WriteDataset(MemoryMarshal.AsBytes(lengthArray));

            Span<WritingGlobalHeapId> gheapIdArray
                = stackalloc WritingGlobalHeapId[] { globalHeapId };

            target.WriteDataset(MemoryMarshal.AsBytes(gheapIdArray));
        }

        return (message, encode);
    }

    private static (DatatypeMessage, ElementEncodeDelegate) GetTypeInfoForVariableLengthString(
        NativeWriteContext context)
    {
        var (baseMessage, baseEncode) = GetTypeInfoForScalar(context, typeof(byte));

        var message = new DatatypeMessage(
            VLEN_REFERENCE_SIZE,
            new VariableLengthBitFieldDescription(
                InternalVariableLengthType.String,
                PaddingType.NullPad,
                CharacterSetEncoding.UTF8
            ),
            [
                new VariableLengthPropertyDescription(
                    baseMessage
                )
            ]
        )
        {
            Version = DATATYPE_MESSAGE_VERSION,
            Class = DatatypeMessageClass.VariableLength
        };

        void encode(object source, IH5WriteStream target)
        {
            var globalHeapId = default(WritingGlobalHeapId);
            Span<int> lengthArray = stackalloc int[1];

            if (source is not null)
            {
                string stringData = (string)source;
                int stringLength = Encoding.UTF8.GetByteCount(stringData);

                lengthArray[0] = stringLength;

                (globalHeapId, var memory) = context.GlobalHeapManager
                    .AddObject(stringLength);

                context.ShortlivedStream.Reset(memory);

                // TODO can array creation be avoided here?
                byte[] bytes = Encoding.UTF8.GetBytes(stringData);
                context.ShortlivedStream.WriteDataset(bytes);
            }

            // encode variable length object
            target.WriteDataset(MemoryMarshal.AsBytes(lengthArray));

            Span<WritingGlobalHeapId> gheapIdArray
                = stackalloc WritingGlobalHeapId[] { globalHeapId };

            target.WriteDataset(MemoryMarshal.AsBytes(gheapIdArray));
        }

        return (message, encode);
    }

    /// <summary>
    ///     A fixed-length string datatype of <paramref name="length" /> bytes.
    /// </summary>
    /// <remarks>
    ///     <paramref name="padding" /> says what the unused bytes of the field mean. A measured width passes
    ///     <see cref="PaddingType.NullPad" />; a width the caller declared keeps
    ///     <see cref="PaddingType.NullTerminate" />.
    ///     <para>
    ///         The choice does not change what a reader gets. There is no conversion path between fixed- and
    ///         variable-length strings in the C library at all, so every consumer reads the field into a
    ///         fixed-width buffer, and those bytes are identical either way - a value shorter than the field is
    ///         followed by nulls whatever the declaration says, and stops a C string at the same place.
    ///     </para>
    ///     <para>
    ///         What it changes is writing. The C library reserves the final byte of a NullTerminate field when
    ///         it converts a value into one, so a tool rewriting the field through a wider datatype lands
    ///         length - 1 bytes and drops the last character of a value that fills it. A measured width is
    ///         filled to its last byte by the longest element by construction, so NullTerminate would put
    ///         exactly that element at risk. NullPad leaves the whole width writable.
    ///     </para>
    ///     <para>
    ///         The cost is that <c>h5dump</c> honours NullPad literally and prints a shorter value with its
    ///         padding attached, as <c>"Wafer\000\000"</c>. That is a rendering difference in one tool, not
    ///         something a reader sees.
    ///     </para>
    /// </remarks>
    private static (DatatypeMessage, ElementEncodeDelegate) GetTypeInfoForFixedLengthString(
        NativeWriteContext context, int length, PaddingType padding = PaddingType.NullTerminate)
    {
        var message = new DatatypeMessage(
            (uint)length,
            new StringBitFieldDescription(
                padding,
                CharacterSetEncoding.UTF8
            ),
            Array.Empty<DatatypePropertyDescription>()
        )
        {
            Version = DATATYPE_MESSAGE_VERSION,
            Class = DatatypeMessageClass.String
        };

        void encode(object source, IH5WriteStream target)
        {
            string? value = (string?)source;

            // Refused rather than emptied. A fixed-length field has no way to hold the difference between
            // null and an empty string, and writing one as the other loses the distinction with nothing to
            // show for it - the same reason StringOverflow.Throw exists for a value that does not fit.
            if (value is null)
                throw new InvalidOperationException(
                    $"A null string does not fit a fixed-length string of {length} bytes, which has no way "
                    + "to represent the difference between null and an empty string. Write variable-length "
                    + "strings instead - H5WriteOptions.DefaultStringLength of 0, or "
                    + "H5AttributeStringLength.VariableLength for an attribute - or replace the null with an "
                    + "empty string.");

            var stringBytes = Encoding.UTF8
                .GetBytes(value)
                .AsSpan();

            if (stringBytes.Length > length)
            {
                if (context.WriteOptions.StringOverflow == H5StringOverflow.Throw)
                    throw new InvalidOperationException(
                        $"The string '{value}' needs {stringBytes.Length} UTF-8 bytes and does not "
                        + $"fit a fixed-length string of {length} bytes. Note that an HDF5 string width is "
                        + "in BYTES, not characters, so a width counted in characters is too small for any "
                        + "value outside ASCII. Set H5WriteOptions.StringOverflow to Truncate to discard "
                        + "the excess instead.");

                stringBytes = stringBytes[..length];
            }

            target.WriteDataset(stringBytes);

            int padding = length - stringBytes.Length;

            if (padding > 0)
            {
                if (padding < 256)
                {
                    Span<byte> paddingBuffer = stackalloc byte[padding];
                    paddingBuffer.Clear();
                    target.WriteDataset(paddingBuffer);
                }

                else
                {
                    using var paddingBufferOwner = MemoryPool<byte>.Shared.Rent(padding);

                    var paddingBuffer = paddingBufferOwner.Memory.Span[..padding];
                    paddingBuffer.Clear();

                    target.WriteDataset(paddingBuffer);
                }
            }
        }

        return (message, encode);
    }

    private static (DatatypeMessage, ElementEncodeDelegate) GetTypeInfoForObjectReference(
        NativeWriteContext context)
    {
        var message = new DatatypeMessage(
            (uint)Unsafe.SizeOf<NativeObjectReference1>(),
            new ReferenceBitFieldDescription(
                InternalReferenceType.ObjectReference
            ),
            []
        )
        {
            Version = DATATYPE_MESSAGE_VERSION,
            Class = DatatypeMessageClass.Reference
        };

        void encode(object source, IH5WriteStream target)
        {
            var objectReference = (H5ObjectReference)source;

            if (!context.ObjectToAddressMap
                    .TryGetValue(objectReference.ReferencedObject, out ulong address))
            {
                context.ObjectToAddressMap[objectReference.ReferencedObject] = default;

                if (objectReference.ReferencedObject is H5Group group)
                    address = context.Writer.EncodeGroup(group);

                else if (objectReference.ReferencedObject is H5Dataset dataset)
                    address = context.Writer.EncodeDataset(dataset);

                else
                    throw new Exception("Named data types cannot yet be used in combination with H5ObjectReference.");

                context.ObjectToAddressMap[objectReference.ReferencedObject] = address;
            }

            else if (address == default)
            {
                throw new Exception("The current object is already being encoded which suggests a circular reference.");
            }

            Span<ulong> buffer = stackalloc ulong[] { address };

            target.WriteDataset(MemoryMarshal.AsBytes(buffer));
        }

        return (message, encode);
    }

    private static (DatatypeMessage, ElementEncodeDelegate) GetTypeInfoForOpaque(
        H5OpaqueInfo opaqueInfo)
    {
        var message = new DatatypeMessage(
            opaqueInfo.TypeSize,
            new OpaqueBitFieldDescription(
                (byte)MathUtils.Ceil_N(opaqueInfo.Tag.Length + 1, 8)
            ),
            [
                new OpaquePropertyDescription(opaqueInfo.Tag)
            ]
        )
        {
            Version = DATATYPE_MESSAGE_VERSION,
            Class = DatatypeMessageClass.Opaque
        };

        void encode(object source, IH5WriteStream target)
        {
            // do nothing
        }

        return (message, encode);
    }

    private static (DatatypeMessage, ElementEncodeDelegate) GetTypeInfoForUnsignedFixedPointTypes(
        Type type,
        ByteOrder endianness)
    {
        var message = new DatatypeMessage(
            (uint)Marshal.SizeOf(type),
            new FixedPointBitFieldDescription(
                endianness,
                default,
                default,
                false
            ),
            [
                new FixedPointPropertyDescription(0,
                    (ushort)(Marshal.SizeOf(type) * 8)
                )
            ]
        )
        {
            Version = DATATYPE_MESSAGE_VERSION,
            Class = DatatypeMessageClass.FixedPoint
        };

        var invokeEncodeUnmanagedElement = WriteUtils.MethodInfoEncodeUnmanagedElement.MakeGenericMethod(type);
        object[] parameters = new object[2];

        void encode(object source, IH5WriteStream target)
        {
            parameters[0] = source;
            parameters[1] = target;
            invokeEncodeUnmanagedElement.Invoke(default, parameters);
        }

        ;

        return (message, encode);
    }

    private static (DatatypeMessage, ElementEncodeDelegate) GetTypeInfoForSignedFixedPointTypes(
        Type type,
        ByteOrder endianness)
    {
        var message = new DatatypeMessage(
            (uint)Marshal.SizeOf(type),
            new FixedPointBitFieldDescription(
                endianness,
                default,
                default,
                true
            ),
            [
                new FixedPointPropertyDescription(0,
                    (ushort)(Marshal.SizeOf(type) * 8)
                )
            ]
        )
        {
            Version = DATATYPE_MESSAGE_VERSION,
            Class = DatatypeMessageClass.FixedPoint
        };

        var invokeEncodeUnmanagedElement = WriteUtils.MethodInfoEncodeUnmanagedElement.MakeGenericMethod(type);
        object[] parameters = new object[2];

        void encode(object source, IH5WriteStream target)
        {
            parameters[0] = source;
            parameters[1] = target;
            invokeEncodeUnmanagedElement.Invoke(default, parameters);
        }

        ;

        return (message, encode);
    }

    private static (DatatypeMessage, ElementEncodeDelegate) GetTypeInfoFor16BitFloatingPoint(
        Type type,
        ByteOrder endianness)
    {
        var message = new DatatypeMessage(
            (uint)Unsafe.SizeOf<Half>(),
            new FloatingPointBitFieldDescription(
                endianness,
                default,
                default,
                default,
                MantissaNormalization.MsbIsNotStoredButImplied,
                15
            ),

            // https://en.wikipedia.org/wiki/IEEE_754#Basic_and_interchange_formats
            [
                new FloatingPointPropertyDescription(0,
                    16,
                    10,
                    5,
                    0,
                    10,
                    15
                )
            ]
        )
        {
            Version = DATATYPE_MESSAGE_VERSION,
            Class = DatatypeMessageClass.FloatingPoint
        };

        static void encode(object source, IH5WriteStream target)
        {
            Span<Half> data = stackalloc Half[] { (Half)source };
            target.WriteDataset(MemoryMarshal.AsBytes(data));
        }

        ;

        return (message, encode);
    }

    private static (DatatypeMessage, ElementEncodeDelegate) GetTypeInfoFor32BitFloatingPoint(
        Type type,
        ByteOrder endianness)
    {
        var message = new DatatypeMessage(
            sizeof(float),
            new FloatingPointBitFieldDescription(
                endianness,
                default,
                default,
                default,
                MantissaNormalization.MsbIsNotStoredButImplied,
                31
            ),

            // https://en.wikipedia.org/wiki/IEEE_754#Basic_and_interchange_formats
            [
                new FloatingPointPropertyDescription(0,
                    32,
                    23,
                    8,
                    0,
                    23,
                    127
                )
            ]
        )
        {
            Version = DATATYPE_MESSAGE_VERSION,
            Class = DatatypeMessageClass.FloatingPoint
        };

        static void encode(object source, IH5WriteStream target)
        {
            Span<float> data = stackalloc float[] { (float)source };
            target.WriteDataset(MemoryMarshal.AsBytes(data));
        }

        ;

        return (message, encode);
    }

    private static (DatatypeMessage, ElementEncodeDelegate) GetTypeInfoFor64BitFloatingPoint(
        Type type,
        ByteOrder endianness)
    {
        var message = new DatatypeMessage(
            sizeof(double),
            new FloatingPointBitFieldDescription(
                endianness,
                default,
                default,
                default,
                MantissaNormalization.MsbIsNotStoredButImplied,
                63
            ),

            // https://en.wikipedia.org/wiki/IEEE_754#Basic_and_interchange_formats
            [
                new FloatingPointPropertyDescription(0,
                    64,
                    52,
                    11,
                    0,
                    52,
                    1023
                )
            ]
        )
        {
            Version = DATATYPE_MESSAGE_VERSION,
            Class = DatatypeMessageClass.FloatingPoint
        };

        static void encode(object source, IH5WriteStream target)
        {
            Span<double> data = stackalloc double[] { (double)source };
            target.WriteDataset(MemoryMarshal.AsBytes(data));
        }

        ;

        return (message, encode);
    }

    private static (DatatypeMessage, EncodeDelegate<T>) GetTypeInfoForTopLevelDictionary<T>(
        NativeWriteContext context,
        IDictionary topLevelData)
    {
        var elementType = topLevelData.GetType().GenericTypeArguments[1];
        var (valueMessage, valueEncode) = GetTypeInfoForScalar(context, elementType);
        ushort memberCount = (ushort)topLevelData.Count;
        uint memberSize = valueMessage.Size;

        var propertyDescriptions = new CompoundPropertyDescription[memberCount];
        ulong offset = 0UL;
        int index = 0;

        foreach (DictionaryEntry entry in topLevelData)
        {
            string key = (string)entry.Key;

            var propertyDescription = new CompoundPropertyDescription(
                key,
                offset,
                valueMessage
            );

            offset += memberSize;

            propertyDescriptions[index] = propertyDescription;
            index++;
        }

        var message = new DatatypeMessage(
            valueMessage.Size * memberCount,
            new CompoundBitFieldDescription(
                memberCount
            ),
            propertyDescriptions
        )
        {
            Version = DATATYPE_MESSAGE_VERSION,
            Class = DatatypeMessageClass.Compound
        };

        void encode(Memory<T> source, IH5WriteStream target)
        {
            foreach (object? value in topLevelData.Values) valueEncode(value, target);
        }

        return (message, encode);
    }

    private static (DatatypeMessage, EncodeDelegate<T>) GetTypeInfoForTopLevelMemory<T>(
        NativeWriteContext context,
        H5OpaqueInfo? opaqueInfo,
        int? stringLength = default,
        PaddingType stringPadding = PaddingType.NullTerminate)
    {
        var (message, elementEncode) =
            GetTypeInfoForScalar(context, typeof(T), stringLength, opaqueInfo, stringPadding);

        void encode(Memory<T> source, IH5WriteStream target)
        {
            var sourceSpan = source.Span;

            for (int i = 0; i < source.Length; i++) elementEncode(sourceSpan[i]!, target);
        }

        ;

        return (message, encode);
    }

    private static (DatatypeMessage, EncodeDelegate<T>) GetTypeInfoForTopLevelUnmanagedMemory<T>(
        NativeWriteContext context,
        H5OpaqueInfo? opaqueInfo) where T : struct
    {
        var (message, _) = GetTypeInfoForScalar(context, typeof(T), opaqueInfo: opaqueInfo);

        static void encode(Memory<T> source, IH5WriteStream target)
        {
            target.WriteDataset(MemoryMarshal.AsBytes(source.Span));
        }

        return (message, encode);
    }
}