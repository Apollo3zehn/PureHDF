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

internal delegate void ElementEncodeDelegate(object source, IH5WriteStream target);

internal partial record class DatatypeMessage : Message
{
    private static readonly MethodInfo _methodInfoGetTypeInfoForTopLevelUnmanagedMemory = typeof(DatatypeMessage)
        .GetMethod(nameof(GetTypeInfoForTopLevelUnmanagedMemory), BindingFlags.NonPublic | BindingFlags.Static)!;

    private const int DATATYPE_MESSAGE_VERSION = 3;

    // reference size                = GHEAP address + GHEAP index
    private const int REFERENCE_SIZE = sizeof(ulong) + sizeof(uint);

    // variable length entry size           length
    private const int VLEN_REFERENCE_SIZE = sizeof(uint) + REFERENCE_SIZE;

    public static (DatatypeMessage, EncodeDelegate<T>) Create<T>(
        WriteContext context,
        Memory<T> topLevelData,
        bool isScalar
    )
    {
        var isScalarDictionary = isScalar && typeof(IDictionary)
            .IsAssignableFrom(typeof(T)) && typeof(T).GenericTypeArguments[0] == typeof(string);

        if (isScalar)
            return isScalarDictionary
                ? GetTypeInfoForTopLevelDictionary<T>(context, (IDictionary)topLevelData.Span[0]!)
                : GetTypeInfoForScalar_SpecialEncode<T>(context, stringLength: default);

        else
            return DataUtils.IsReferenceOrContainsReferences(typeof(T))
                ? GetTypeInfoForTopLevelMemory<T>(context)
                : ((DatatypeMessage, EncodeDelegate<T>))_methodInfoGetTypeInfoForTopLevelUnmanagedMemory
                    // TODO cache
                    .MakeGenericMethod(typeof(T))
                    .Invoke(default, new object[] { context })!;
    }

    public override void Encode(H5DriverBase driver)
    {
        var classVersion = (byte)((byte)Class & 0x0F | Version << 4);
        driver.Write(classVersion);

        BitField.Encode(driver);

        driver.Write(Size);

        foreach (var property in Properties)
        {
            property.Encode(driver, Size);
        }
    }

    public override ushort GetEncodeSize()
    {
        var propertiesEncodeSize = Properties.Aggregate(0, (sum, properties)
            => sum + properties.GetEncodeSize(Size));

        var encodeSize =
            sizeof(byte) +
            sizeof(byte) * 3 +
            sizeof(uint) +
            propertiesEncodeSize;

        return (ushort)encodeSize;
    }

    private static (DatatypeMessage, EncodeDelegate<T>) GetTypeInfoForScalar_SpecialEncode<T>(
        WriteContext context,
        int stringLength = default)
    {
        var (dataType, encode) = GetTypeInfoForScalar(context, typeof(T), stringLength);

        void encodeFirstElement(Memory<T> source, IH5WriteStream target)
            => encode(source.Span[0]!, target);

        return (dataType, encodeFirstElement);
    }

    private static (DatatypeMessage, ElementEncodeDelegate) GetTypeInfoForScalar(
        WriteContext context,
        Type type,
        int stringLength = default)
    {
        if (stringLength == default)
            stringLength = context.SerializerOptions.DefaultStringLength;

        var cache = context.TypeToMessageMap;

        if (cache.TryGetValue(type, out var cachedMessage))
            return cachedMessage;

        var endianness = BitConverter.IsLittleEndian
            ? ByteOrder.LittleEndian
            : ByteOrder.BigEndian;

        (DatatypeMessage newMessage, ElementEncodeDelegate encode) = type switch
        {
            /* string */
            Type when type == typeof(string)
                => stringLength == 0
                    ? GetTypeInfoForVariableLengthString(context)
                    : GetTypeInfoForFixedLengthString(context, stringLength),

            /* dictionary */
            Type when typeof(IDictionary).IsAssignableFrom(type) &&
                        type.GenericTypeArguments[0] == typeof(string)
                => GetTypeInfoForVariableLengthSequence(context, typeof(KeyValuePair<,>)
                    .MakeGenericType(type.GenericTypeArguments)),

            /* array */
            Type when type.IsArray && type.GetElementType() is not null
                => GetTypeInfoForVariableLengthSequence(context, type.GetElementType()!),

            /* generic IEnumerable */
            Type when typeof(IEnumerable).IsAssignableFrom(type) && type.IsGenericType
                => GetTypeInfoForVariableLengthSequence(context, type.GenericTypeArguments[0]),

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
#if NET7_0_OR_GREATER
                || type == typeof(UInt128)
#endif
                => GetTypeInfoForUnsignedFixedPointTypes(type, endianness),

            /* signed fixed-point types */
            Type when
                type == typeof(sbyte) ||
                type == typeof(short) ||
                type == typeof(int) ||
                type == typeof(long)
#if NET7_0_OR_GREATER
                || type == typeof(Int128)
#endif
                => GetTypeInfoForSignedFixedPointTypes(type, endianness),

#if NET5_0_OR_GREATER
            /* 16 bit floating-point */
            Type when type == typeof(Half)
                => GetTypeInfoFor16BitFloatingPoint(type, endianness),
#endif

            /* 32 bit floating-point */
            Type when type == typeof(float)
                => GetTypeInfoFor32BitFloatingPoint(type, endianness),

            /* 64 bit floating-point */
            Type when type == typeof(double)
                => GetTypeInfoFor64BitFloatingPoint(type, endianness),

            /* remaining non-generic value types */
            Type when type.IsValueType && !type.IsGenericType
                => context.SerializerOptions.IncludeStructProperties
                    ? GetTypeInfoForReferenceLikeType(context, type)
                    : GetTypeInfoForValueType(context, type),

            /* remaining generic value types */
            Type when type.IsValueType
                => GetTypeInfoForReferenceLikeType(context, type),

            _ => throw new NotSupportedException($"The data type '{type}' is not supported."),
        };

        cache[type] = (newMessage, encode);
        return (newMessage, encode);
    }

    private static (DatatypeMessage, ElementEncodeDelegate) GetTypeInfoForBool(
        WriteContext context)
    {
        var (baseMessage, _) = GetTypeInfoForScalar(context, typeof(byte));

        static void encode(object source, IH5WriteStream target)
        {
            Span<byte> buffer = stackalloc byte[]
            {
                ((bool)source) ? (byte)1 : (byte)0
            };

            target.Write(buffer);
        }

        return (baseMessage, encode);
    }

    private static (DatatypeMessage, ElementEncodeDelegate) GetTypeInfoForEnum(
        WriteContext context,
        Type type)
    {
        var underlyingType = Enum.GetUnderlyingType(type);
        var enumValues = Enum.GetValues(type);
        var enumObjects = new object[enumValues.Length];

        for (int i = 0; i < enumValues.Length; i++)
        {
            enumObjects[i] = enumValues.GetValue(i)!;
        }

        var values = (underlyingType switch
        {
            Type t when t == typeof(byte) => enumObjects.Select(enumValue => new byte[] { (byte)enumValue }),
            Type t when t == typeof(sbyte) => enumObjects.Select(enumValue => new byte[] { unchecked((byte)enumValue) }),
            Type t when t == typeof(ushort) => enumObjects.Select(enumValue => BitConverter.GetBytes((ushort)enumValue)),
            Type t when t == typeof(short) => enumObjects.Select(enumValue => BitConverter.GetBytes((short)enumValue)),
            Type t when t == typeof(uint) => enumObjects.Select(enumValue => BitConverter.GetBytes((uint)enumValue)),
            Type t when t == typeof(int) => enumObjects.Select(enumValue => BitConverter.GetBytes((int)enumValue)),
            Type t when t == typeof(ulong) => enumObjects.Select(enumValue => BitConverter.GetBytes((ulong)enumValue)),
            Type t when t == typeof(long) => enumObjects.Select(enumValue => BitConverter.GetBytes((long)enumValue)),
            _ => throw new Exception($"The enum type {underlyingType} is not supported.")
        }).ToArray();

        var (baseMessage, baseEncode) = GetTypeInfoForScalar(context, Enum.GetUnderlyingType(type));

        var properties = new EnumerationPropertyDescription(
            BaseType: baseMessage,
            Names: Enum.GetNames(type),
            Values: values
        );

        var message = new DatatypeMessage(
            baseMessage.Size,

            new EnumerationBitFieldDescription(
                MemberCount: (ushort)Enum.GetNames(type).Length
            ),

            new EnumerationPropertyDescription[] {
                properties
            }
        )
        {
            Version = DATATYPE_MESSAGE_VERSION,
            Class = DatatypeMessageClass.Enumerated
        };

        return (message, baseEncode);
    }

    private static (DatatypeMessage, ElementEncodeDelegate) GetTypeInfoForValueType(
        WriteContext context,
        Type type)
    {
        var fieldInfos = type.GetFields(BindingFlags.Public | BindingFlags.Instance);
        var properties = new CompoundPropertyDescription[fieldInfos.Length];

        for (int i = 0; i < fieldInfos.Length; i++)
        {
            var fieldInfo = fieldInfos[i];
            var underlyingType = fieldInfo.FieldType;
            var (fieldMessage, _) = GetTypeInfoForScalar(context, underlyingType);
            var fieldNameMapper = context.SerializerOptions.FieldNameMapper;

            properties[i] = new CompoundPropertyDescription(
                Name: fieldNameMapper is null ? fieldInfo.Name : fieldNameMapper(fieldInfo) ?? fieldInfo.Name,
                MemberByteOffset: (ulong)Marshal.OffsetOf(type, fieldInfo.Name),
                MemberTypeMessage: fieldMessage
            );
        }

        var bitfield = new CompoundBitFieldDescription(
            MemberCount: (ushort)fieldInfos.Length
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

        var invokeEncodeUnmanagedElement = WriteUtils.MethodInfoElement.MakeGenericMethod(type);
        var parameters = new object[2];

        void encode(object source, IH5WriteStream target)
        {
            parameters[0] = source;
            parameters[1] = target;
            invokeEncodeUnmanagedElement.Invoke(default, parameters);
        };

        return (message, encode);
    }

    private static (DatatypeMessage, ElementEncodeDelegate) GetTypeInfoForReferenceLikeType(
        WriteContext context,
        Type type)
    {
        CompoundBitFieldDescription bitfield;

        var offset = 0U;
        var isValueType = type.IsValueType;
        var defaultStringLength = context.SerializerOptions.DefaultStringLength;

        // fields
        var includeFields = isValueType
            ? context.SerializerOptions.IncludeStructFields
            : context.SerializerOptions.IncludeClassFields;

        var fieldInfos = includeFields
            ? type.GetFields(BindingFlags.Public | BindingFlags.Instance)
            : Array.Empty<FieldInfo>();

        var fieldEncodes = includeFields
            ? new ElementEncodeDelegate[fieldInfos.Length]
            : Array.Empty<ElementEncodeDelegate>();

        var fieldNameMapper = context.SerializerOptions.FieldNameMapper;
        var fieldStringLengthMapper = context.SerializerOptions.FieldStringLengthMapper;

        // properties
        var includeProperties = isValueType
            ? context.SerializerOptions.IncludeStructProperties
            : context.SerializerOptions.IncludeClassProperties;

        var propertyInfos = type
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(propertyInfo => propertyInfo.CanRead)
            .ToArray();

        var propertyEncodes = includeProperties
            ? new ElementEncodeDelegate[propertyInfos.Length]
            : Array.Empty<ElementEncodeDelegate>();

        var propertyNameMapper = context.SerializerOptions.PropertyNameMapper;
        var propertyStringLengthMapper = context.SerializerOptions.PropertyStringLengthMapper;

        // bitfield
        bitfield = new CompoundBitFieldDescription(
            MemberCount: (ushort)(fieldInfos.Length + propertyInfos.Length)
        );

        /* H5Odtype.c (H5O_dtype_decode_helper: case H5T_COMPOUND) */
        if (bitfield.MemberCount == 0)
            throw new Exception("The compound data type needs at least one member");

        // propertyDescriptions
        var properties = new CompoundPropertyDescription[bitfield.MemberCount];

        if (includeFields)
        {
            for (int i = 0; i < fieldInfos.Length; i++)
            {
                var fieldInfo = fieldInfos[i];
                var underlyingType = fieldInfo.FieldType;

                var stringLength = underlyingType == typeof(string)
                    ? fieldStringLengthMapper is null ? defaultStringLength : fieldStringLengthMapper(fieldInfo) ?? defaultStringLength
                    : defaultStringLength;

                var (fieldMessage, fieldEncode) = GetTypeInfoForScalar(context, underlyingType, stringLength: stringLength);

                fieldEncodes[i] = fieldEncode;

                properties[i] = new CompoundPropertyDescription(
                    Name: fieldNameMapper is null ? fieldInfo.Name : fieldNameMapper(fieldInfo) ?? fieldInfo.Name,
                    MemberByteOffset: offset,
                    MemberTypeMessage: fieldMessage
                );

                offset += fieldMessage.Size;
            }
        }

        if (includeProperties)
        {
            for (int i = 0; i < propertyInfos.Length; i++)
            {
                var propertyInfo = propertyInfos[i];
                var underlyingType = propertyInfo.PropertyType;

                var stringLength = underlyingType == typeof(string)
                    ? propertyStringLengthMapper is null ? defaultStringLength : propertyStringLengthMapper(propertyInfo) ?? defaultStringLength
                    : defaultStringLength;

                var (propertyMessage, propertyEncode) = GetTypeInfoForScalar(context, underlyingType, stringLength: stringLength);

                propertyEncodes[i] = propertyEncode;

                properties[fieldInfos.Length + i] = new CompoundPropertyDescription(
                    Name: propertyNameMapper is null ? propertyInfo.Name : propertyNameMapper(propertyInfo) ?? propertyInfo.Name,
                    MemberByteOffset: offset,
                    MemberTypeMessage: propertyMessage
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
                var typeSize = (int)properties[i].MemberTypeMessage.Size;
                var fieldInfo = fieldInfos[i];

                memberEncode(fieldInfo.GetValue(source)!, target);
            }

            // properties
            for (int i = 0; i < propertyEncodes.Length; i++)
            {
                var memberEncode = propertyEncodes[i];
                var typeSize = (int)properties[i].MemberTypeMessage.Size;
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

    private static (DatatypeMessage, ElementEncodeDelegate) GetTypeInfoForVariableLengthSequence(
        WriteContext context,
        Type baseType)
    {
        var (baseMessage, baseEncode) = GetTypeInfoForScalar(context, baseType);

        var message = new DatatypeMessage(

            VLEN_REFERENCE_SIZE,

            new VariableLengthBitFieldDescription(
                Type: InternalVariableLengthType.Sequence,
                PaddingType: default,
                Encoding: default
            ),

            new VariableLengthPropertyDescription[] {
                new (
                    BaseType: baseMessage
                )
            }
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
                var itemCount = WriteUtils.GetEnumerableLength(enumerable);

                var typeSize = ((VariableLengthPropertyDescription)message.Properties[0])
                    .BaseType
                    .Size;

                var totalLength = (int)typeSize * itemCount;
                lengthArray[0] = itemCount;

                (globalHeapId, var localTarget) = context.GlobalHeapManager
                    .AddObject(totalLength);

                // encode items
                foreach (var item in enumerable)
                {
                    baseEncode(item, localTarget);
                }
            }

            // encode variable length object
            target.Write(MemoryMarshal.AsBytes(lengthArray));

            Span<WritingGlobalHeapId> gheapIdArray
                = stackalloc WritingGlobalHeapId[] { globalHeapId };

            target.Write(MemoryMarshal.AsBytes(gheapIdArray));
        }

        return (message, encode);
    }

    private static (DatatypeMessage, ElementEncodeDelegate) GetTypeInfoForVariableLengthString(
        WriteContext context)
    {
        var (baseMessage, baseEncode) = GetTypeInfoForScalar(context, typeof(byte));

        var message = new DatatypeMessage(

            VLEN_REFERENCE_SIZE,

            new VariableLengthBitFieldDescription(
                Type: InternalVariableLengthType.String,
                PaddingType: PaddingType.NullPad,
                Encoding: CharacterSetEncoding.UTF8
            ),

            new VariableLengthPropertyDescription[] {
                new (
                    BaseType: baseMessage
                )
            }
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
                var stringData = (string)source;
                var stringLength = Encoding.UTF8.GetByteCount(stringData);

                lengthArray[0] = stringLength;

                (globalHeapId, var localTarget) = context.GlobalHeapManager
                    .AddObject(stringLength);

                // TODO can array creation be avoided here?
                var bytes = Encoding.UTF8.GetBytes(stringData);
                localTarget.Write(bytes);
            }

            // encode variable length object
            target.Write(MemoryMarshal.AsBytes(lengthArray));

            Span<WritingGlobalHeapId> gheapIdArray
                = stackalloc WritingGlobalHeapId[] { globalHeapId };

            target.Write(MemoryMarshal.AsBytes(gheapIdArray));
        }

        return (message, encode);
    }

    private static (DatatypeMessage, ElementEncodeDelegate) GetTypeInfoForFixedLengthString(
        WriteContext context, int length)
    {
        var message = new DatatypeMessage(

            (uint)length,

            new StringBitFieldDescription(
                PaddingType: PaddingType.NullPad,
                Encoding: CharacterSetEncoding.UTF8
            ),

            Array.Empty<DatatypePropertyDescription>()
        )
        {
            Version = DATATYPE_MESSAGE_VERSION,
            Class = DatatypeMessageClass.String
        };

        void encode(object source, IH5WriteStream target)
        {
            var stringBytes = Encoding.UTF8
                .GetBytes((string)source)
                .AsSpan();

            var truncate = Math.Min(stringBytes.Length, length);
            stringBytes = stringBytes[..truncate];

            target.Write(stringBytes);

            var padding = length - stringBytes.Length;

            if (padding > 0)
            {
                if (padding < 256)
                {
                    Span<byte> paddingBuffer = stackalloc byte[padding];
                    paddingBuffer.Clear();
                    target.Write(paddingBuffer);
                }

                else
                {
                    using var paddingBufferOwner = MemoryPool<byte>.Shared.Rent(padding);
                    target.Write(paddingBufferOwner.Memory.Span[..padding]);
                }
            }
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
                ByteOrder: endianness,
                PaddingTypeLow: default,
                PaddingTypeHigh: default,
                IsSigned: false
            ),

            new FixedPointPropertyDescription[] {
                new(BitOffset: 0,
                    BitPrecision: (ushort)(Marshal.SizeOf(type) * 8)
                )
            }
        )
        {
            Version = DATATYPE_MESSAGE_VERSION,
            Class = DatatypeMessageClass.FixedPoint
        };

        var invokeEncodeUnmanagedElement = WriteUtils.MethodInfoElement.MakeGenericMethod(type);
        var parameters = new object[2];

        void encode(object source, IH5WriteStream target)
        {
            parameters[0] = source;
            parameters[1] = target;
            invokeEncodeUnmanagedElement.Invoke(default, parameters);
        };

        return (message, encode);
    }

    private static (DatatypeMessage, ElementEncodeDelegate) GetTypeInfoForSignedFixedPointTypes(
        Type type,
        ByteOrder endianness)
    {
        var message = new DatatypeMessage(

            (uint)Marshal.SizeOf(type),

            new FixedPointBitFieldDescription(
                ByteOrder: endianness,
                PaddingTypeLow: default,
                PaddingTypeHigh: default,
                IsSigned: true
            ),

            new FixedPointPropertyDescription[] {
                new(BitOffset: 0,
                    BitPrecision: (ushort)(Marshal.SizeOf(type) * 8)
                )
            }
        )
        {
            Version = DATATYPE_MESSAGE_VERSION,
            Class = DatatypeMessageClass.FixedPoint
        };

        var invokeEncodeUnmanagedElement = WriteUtils.MethodInfoElement.MakeGenericMethod(type);
        var parameters = new object[2];

        void encode(object source, IH5WriteStream target)
        {
            parameters[0] = source;
            parameters[1] = target;
            invokeEncodeUnmanagedElement.Invoke(default, parameters);
        };

        return (message, encode);
    }

#if NET5_0_OR_GREATER
    private static (DatatypeMessage, ElementEncodeDelegate) GetTypeInfoFor16BitFloatingPoint(
        Type type,
        ByteOrder endianness)
    {
        var message = new DatatypeMessage(

            (uint)Unsafe.SizeOf<Half>(),

            new FloatingPointBitFieldDescription(
                ByteOrder: endianness,
                PaddingTypeLow: default,
                PaddingTypeHigh: default,
                PaddingTypeInternal: default,
                MantissaNormalization: MantissaNormalization.MsbIsNotStoredButImplied,
                SignLocation: 15
            ),

            // https://en.wikipedia.org/wiki/IEEE_754#Basic_and_interchange_formats
            new FloatingPointPropertyDescription[] {
                new(BitOffset: 0,
                    BitPrecision: 16,
                    ExponentLocation: 10,
                    ExponentSize: 5,
                    MantissaLocation: 0,
                    MantissaSize: 10,
                    ExponentBias: 15
                )
            }
        )
        {
            Version = DATATYPE_MESSAGE_VERSION,
            Class = DatatypeMessageClass.FloatingPoint
        };

        static void encode(object source, IH5WriteStream target)
        {
            Span<Half> data = stackalloc Half[] { (Half)source };
            target.Write(MemoryMarshal.AsBytes(data));
        };

        return (message, encode);
    }
#endif

    private static (DatatypeMessage, ElementEncodeDelegate) GetTypeInfoFor32BitFloatingPoint(
        Type type,
        ByteOrder endianness)
    {
        var message = new DatatypeMessage(

            sizeof(float),

            new FloatingPointBitFieldDescription(
                ByteOrder: endianness,
                PaddingTypeLow: default,
                PaddingTypeHigh: default,
                PaddingTypeInternal: default,
                MantissaNormalization: MantissaNormalization.MsbIsNotStoredButImplied,
                SignLocation: 31
            ),

            // https://en.wikipedia.org/wiki/IEEE_754#Basic_and_interchange_formats
            new FloatingPointPropertyDescription[] {
                new(BitOffset: 0,
                    BitPrecision: 32,
                    ExponentLocation: 23,
                    ExponentSize: 8,
                    MantissaLocation: 0,
                    MantissaSize: 23,
                    ExponentBias: 127
                )
            }
        )
        {
            Version = DATATYPE_MESSAGE_VERSION,
            Class = DatatypeMessageClass.FloatingPoint
        };

        static void encode(object source, IH5WriteStream target)
        {
            Span<float> data = stackalloc float[] { (float)source };
            target.Write(MemoryMarshal.AsBytes(data));
        };

        return (message, encode);
    }

    private static (DatatypeMessage, ElementEncodeDelegate) GetTypeInfoFor64BitFloatingPoint(
        Type type,
        ByteOrder endianness)
    {
        var message = new DatatypeMessage(

            sizeof(double),

            new FloatingPointBitFieldDescription(
                ByteOrder: endianness,
                PaddingTypeLow: default,
                PaddingTypeHigh: default,
                PaddingTypeInternal: default,
                MantissaNormalization: MantissaNormalization.MsbIsNotStoredButImplied,
                SignLocation: 63
            ),

            // https://en.wikipedia.org/wiki/IEEE_754#Basic_and_interchange_formats
            new FloatingPointPropertyDescription[] {
                new(BitOffset: 0,
                    BitPrecision: 64,
                    ExponentLocation: 52,
                    ExponentSize: 11,
                    MantissaLocation: 0,
                    MantissaSize: 52,
                    ExponentBias: 1023
                )
            }
        )
        {
            Version = DATATYPE_MESSAGE_VERSION,
            Class = DatatypeMessageClass.FloatingPoint
        };

        static void encode(object source, IH5WriteStream target)
        {
            Span<double> data = stackalloc double[] { (double)source };
            target.Write(MemoryMarshal.AsBytes(data));
        };

        return (message, encode);
    }

    private static (DatatypeMessage, EncodeDelegate<T>) GetTypeInfoForTopLevelDictionary<T>(
        WriteContext context,
        IDictionary topLevelData)
    {
        var elementType = topLevelData.GetType().GenericTypeArguments[1];
        var (valueMessage, valueEncode) = GetTypeInfoForScalar(context, elementType);
        var memberCount = (ushort)topLevelData.Count;
        var memberSize = valueMessage.Size;

        var propertyDescriptions = new CompoundPropertyDescription[memberCount];
        var offset = 0UL;
        var index = 0;

        foreach (DictionaryEntry entry in topLevelData)
        {
            var key = (string)entry.Key;

            var propertyDescription = new CompoundPropertyDescription(
                Name: key,
                MemberByteOffset: offset,
                MemberTypeMessage: valueMessage
            );

            offset += memberSize;

            propertyDescriptions[index] = propertyDescription;
            index++;
        }

        var message = new DatatypeMessage(

            valueMessage.Size * memberCount,

            new CompoundBitFieldDescription(
                MemberCount: memberCount
            ),

            propertyDescriptions
        )
        {
            Version = DATATYPE_MESSAGE_VERSION,
            Class = DatatypeMessageClass.Compound
        };

        void encode(Memory<T> source, IH5WriteStream target)
        {
            foreach (var value in topLevelData.Values)
            {
                valueEncode(value, target);
            }
        }

        return (message, encode);
    }

    private static (DatatypeMessage, EncodeDelegate<T>) GetTypeInfoForTopLevelMemory<T>(
        WriteContext context)
    {
        var (message, elementEncode) = GetTypeInfoForScalar(context, typeof(T));

        void encode(Memory<T> source, IH5WriteStream target)
        {
            var sourceSpan = source.Span;

            for (int i = 0; i < source.Length; i++)
            {
                elementEncode(sourceSpan[i]!, target);
            }
        };

        return (message, encode);
    }

    private static (DatatypeMessage, EncodeDelegate<T>) GetTypeInfoForTopLevelUnmanagedMemory<T>(
        WriteContext context) where T : struct
    {
        var (message, _) = GetTypeInfoForScalar(context, typeof(T));

        static void encode(Memory<T> source, IH5WriteStream target)
            => target.Write(MemoryMarshal.AsBytes(source.Span));

        return (message, encode);
    }
}