using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using PureHDF.Experimental;

namespace PureHDF.VOL.Native;

// TODO: use this for generic structs https://github.com/SergeyTeplyakov/ObjectLayoutInspector?

internal delegate void EncodeDelegate(Stream driver, object data);

internal partial record class DatatypeMessage : Message
{
    private const int DATATYPE_MESSAGE_VERSION = 3;

    // reference size                = GHEAP address + GHEAP index
    private const int REFERENCE_SIZE = sizeof(ulong) + sizeof(uint);

    // variable length entry size           length
    private const int VLEN_REFERENCE_SIZE = sizeof(uint) + REFERENCE_SIZE;

    public static (DatatypeMessage, ulong[] dimensions, EncodeDelegate) Create(
        WriteContext context,
        Type type,
        object topLevelData
    )
    {
        var result = type switch
        {
            /* dictionary */
            Type when typeof(IDictionary).IsAssignableFrom(type) && type.GenericTypeArguments[0] == typeof(string)
                => GetTypeInfoForTopLevelDictionary(context, type, (IDictionary)topLevelData),

            /* array */
            Type when WriteUtils.IsArray(type)
                => ReadUtils.IsReferenceOrContainsReferences(type.GetElementType()!)
                    ? GetTypeInfoForTopLevelEnumerable(context, type, (IEnumerable)topLevelData, isArray: true)
                    : GetTypeInfoForTopLevelUnmanagedArray(context, type.GetElementType()!, (Array)topLevelData),

            /* Memory<T> */
            Type when WriteUtils.IsMemory(type)
                => ReadUtils.IsReferenceOrContainsReferences(type.GenericTypeArguments[0])
                    ? GetTypeInfoForTopLevelMemory(context, type.GenericTypeArguments[0], topLevelData)
                    : GetTypeInfoForTopLevelUnmanagedMemory(context, type.GenericTypeArguments[0], topLevelData),

            /* generic IEnumerable */
            Type when typeof(IEnumerable).IsAssignableFrom(type) && type.IsGenericType
                => GetTypeInfoForTopLevelEnumerable(context, type, (IEnumerable)topLevelData),

            _ => default
        };

        if (result.Equals(default))
        {
            var singleElementResult = InternalCreate(context, type);
            var dimensions = Array.Empty<ulong>();

            result = (singleElementResult.Item1, dimensions, singleElementResult.Item2);
        }

        return result;
    }

    private static (DatatypeMessage, EncodeDelegate) InternalCreate(
        WriteContext context,
        Type type)
    {
        var cache = context.TypeToMessageMap;

        if (cache.TryGetValue(type, out var cachedMessage))
            return cachedMessage;

        var endianness = BitConverter.IsLittleEndian
            ? ByteOrder.LittleEndian
            : ByteOrder.BigEndian;

        (DatatypeMessage newMessage, EncodeDelegate encode) = type switch
        {
            /* string */
            Type when type == typeof(string)
                => GetTypeInfoForVariableLengthString(context),

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
            Type when ReadUtils.IsReferenceOrContainsReferences(type)
                => GetTypeInfoForReferenceType(context, type),

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
                => GetTypeInfoForUnsignedFixedPointTypes(type, endianness),

            /* signed fixed-point types */
            Type when
                type == typeof(sbyte) ||
                type == typeof(short) ||
                type == typeof(int) ||
                type == typeof(long)
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

            /* 128 bit floating-point */
            Type when type == typeof(decimal)
                => GetTypeInfoFor128BitFloatingPoint(type, endianness),

            /* remaining non-generic value types */
            Type when type.IsValueType && !type.IsGenericType
                => context.SerializerOptions.IncludeStructProperties
                    ? GetTypeInfoForReferenceType(context, type, isValueType: true)
                    : GetTypeInfoForValueType(context, type),

            /* remaining generic value types */
            Type when type.IsValueType
                => GetTypeInfoForReferenceType(context, type, isValueType: true),

            _ => throw new NotSupportedException($"The data type '{type}' is not supported."),
        };

        cache[type] = (newMessage, encode);
        return (newMessage, encode);
    }

    private static (DatatypeMessage, EncodeDelegate) GetTypeInfoForBool(
        WriteContext context)
    {
        var (baseMessage, _) = InternalCreate(context, typeof(byte));

        static void encode(Stream driver, object data)
        {
            Span<byte> buffer = stackalloc byte[] { ((bool)data) ? (byte)1 : (byte)0 };
            driver.Write(buffer);
        }

        return (baseMessage, encode);
    }

    private static (DatatypeMessage, EncodeDelegate) GetTypeInfoForEnum(
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

        var (baseMessage, baseEncode) = InternalCreate(context, Enum.GetUnderlyingType(type));

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

    private static (DatatypeMessage, EncodeDelegate) GetTypeInfoForValueType(
        WriteContext context,
        Type type)
    {
        var fieldInfos = type.GetFields(BindingFlags.Public | BindingFlags.Instance);
        var properties = new CompoundPropertyDescription[fieldInfos.Length];

        for (int i = 0; i < fieldInfos.Length; i++)
        {
            var fieldInfo = fieldInfos[i];
            var underlyingType = fieldInfo.FieldType;
            var (fieldMessage, _) = InternalCreate(context, underlyingType);
            var fieldNameMapper = context.SerializerOptions.FieldNameMapper;

            properties[i] = new CompoundPropertyDescription(
                Name: fieldNameMapper is null ? fieldInfo.Name : fieldNameMapper(fieldInfo),
                MemberByteOffset: (ulong)Marshal.OffsetOf(type, fieldInfo.Name),
                MemberTypeMessage: fieldMessage
            );
        }

        var bitfield = new CompoundBitFieldDescription(
            MemberCount: (ushort)fieldInfos.Length
        );

        var message = new DatatypeMessage(
            (uint)Marshal.SizeOf(type),
            bitfield,
            properties
        )
        {
            Version = DATATYPE_MESSAGE_VERSION,
            Class = DatatypeMessageClass.Compound
        };

        var invokeEncodeUnmanagedElement = _methodInfoElement.MakeGenericMethod(type);
        var parameters = new object[2];

        void encode(Stream driver, object data)
        {
            parameters[0] = driver;
            parameters[1] = data;
            invokeEncodeUnmanagedElement.Invoke(driver, parameters);
        };

        return (message, encode);
    }

    private static (DatatypeMessage, EncodeDelegate) GetTypeInfoForReferenceType(
        WriteContext context,
        Type type,
        bool isValueType = false)
    {
        CompoundBitFieldDescription bitfield;

        var offset = 0U;

        // fields
        var includeFields = isValueType 
            ? context.SerializerOptions.IncludeStructFields
            : context.SerializerOptions.IncludeClassFields;

        var fieldInfos = includeFields
            ? type.GetFields(BindingFlags.Public | BindingFlags.Instance)
            : Array.Empty<FieldInfo>();

        var fieldEncodes = includeFields
            ? new EncodeDelegate[fieldInfos.Length]
            : Array.Empty<EncodeDelegate>();

        var fieldNameMapper = context.SerializerOptions.FieldNameMapper;

        // properties
        var includeProperties = isValueType 
            ? context.SerializerOptions.IncludeStructProperties
            : context.SerializerOptions.IncludeClassProperties;

        var propertyInfos = type
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(propertyInfo => propertyInfo.CanRead)
            .ToArray();

        var propertyEncodes = includeProperties
            ? new EncodeDelegate[propertyInfos.Length]
            : Array.Empty<EncodeDelegate>();

        var propertyNameMapper = context.SerializerOptions.PropertyNameMapper;

        // bitfield
        bitfield = new CompoundBitFieldDescription(
            MemberCount: (ushort)(fieldInfos.Length + propertyInfos.Length)
        );

        // propertyDescriptions
        var properties = new CompoundPropertyDescription[bitfield.MemberCount];

        if (includeFields)
        {
            for (int i = 0; i < fieldInfos.Length; i++)
            {
                var fieldInfo = fieldInfos[i];
                var underlyingType = fieldInfo.FieldType;
                var (fieldMessage, fieldEncode) = InternalCreate(context, underlyingType);

                fieldEncodes[i] = fieldEncode;

                properties[i] = new CompoundPropertyDescription(
                    Name: fieldNameMapper is null ? fieldInfo.Name : fieldNameMapper(fieldInfo),
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
                var (propertyMessage, propertyEncode) = InternalCreate(context, underlyingType);

                propertyEncodes[i] = propertyEncode;

                properties[fieldInfos.Length + i] = new CompoundPropertyDescription(
                    Name: propertyNameMapper is null ? propertyInfo.Name : propertyNameMapper(propertyInfo),
                    MemberByteOffset: offset,
                    MemberTypeMessage: propertyMessage
                );

                offset += propertyMessage.Size;
            }
        }

        void encode(Stream driver, object data)
        {
            // fields
            for (int i = 0; i < fieldEncodes.Length; i++)
            {
                var memberEncode = fieldEncodes[i];
                var typeSize = (int)properties[i].MemberTypeMessage.Size;
                var fieldInfo = fieldInfos[i];

                memberEncode(driver, fieldInfo.GetValue(data)!);
            }

            // properties
            for (int i = 0; i < propertyEncodes.Length; i++)
            {
                var memberEncode = propertyEncodes[i];
                var typeSize = (int)properties[i].MemberTypeMessage.Size;
                var propertyInfo = propertyInfos[i];

                memberEncode(driver, propertyInfo.GetValue(data)!);
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

    private static (DatatypeMessage, EncodeDelegate) GetTypeInfoForVariableLengthSequence(
        WriteContext context,
        Type baseType)
    {
        var (baseMessage, baseEncode) = InternalCreate(context, baseType);

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

        void encode(Stream driver, object data)
        {
            var globalHeapId = default(Experimental.GlobalHeapId);
            Span<int> lengthArray = stackalloc int[1];

            if (data is not null)
            {
                var enumerable = (IEnumerable)data;
                var itemCount = WriteUtils.GetEnumerableLength(enumerable);

                var typeSize = ((VariableLengthPropertyDescription)message.Properties[0])
                    .BaseType
                    .Size;

                var totalLength = (int)typeSize * itemCount;
                lengthArray[0] = itemCount;

                (globalHeapId, var memory) = context.GlobalHeapManager
                    .AddObject(totalLength);

                // encode items
                foreach (var item in enumerable)
                {
                    var localDriver = new MemorySpanStream(memory);

                    baseEncode(localDriver, item);
                    memory = memory[(int)typeSize..];
                }
            }

            // encode variable length object
            driver.Write(MemoryMarshal.AsBytes(lengthArray));

            Span<Experimental.GlobalHeapId> gheapIdArray 
                = stackalloc Experimental.GlobalHeapId[] { globalHeapId };

            driver.Write(MemoryMarshal.AsBytes(gheapIdArray));
        }

        return (message, encode);
    }

    private static (DatatypeMessage, EncodeDelegate) GetTypeInfoForVariableLengthString(
        WriteContext context)
    {
        var (baseMessage, baseEncode) = InternalCreate(context, typeof(byte));

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

        void encode(Stream driver, object data)
        {
            var globalHeapId = default(Experimental.GlobalHeapId);
            Span<int> lengthArray = stackalloc int[1];

            if (data is not null)
            {
                var stringData = (string)data;
                var stringBytes = Encoding.UTF8.GetBytes(stringData);
                lengthArray[0] = stringBytes.Length;

                (globalHeapId, var memory) = context.GlobalHeapManager
                    .AddObject(stringBytes.Length);

                // TODO: optimally no copy operation would be required ... but that requires prior knowledge of string length in bytes
                stringBytes.CopyTo(memory);
            }

            // encode variable length object
            driver.Write(MemoryMarshal.AsBytes(lengthArray));

            Span<Experimental.GlobalHeapId> gheapIdArray 
                = stackalloc Experimental.GlobalHeapId[] { globalHeapId };

            driver.Write(MemoryMarshal.AsBytes(gheapIdArray));
        }

        return (message, encode);
    }

    private static (DatatypeMessage, EncodeDelegate) GetTypeInfoForUnsignedFixedPointTypes(
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

        var invokeEncodeUnmanagedElement = _methodInfoElement.MakeGenericMethod(type);
        var parameters = new object[2];

        void encode(Stream driver, object data)
        {
            parameters[0] = driver;
            parameters[1] = data;
            invokeEncodeUnmanagedElement.Invoke(driver, parameters);
        };

        return (message, encode);
    }

    private static (DatatypeMessage, EncodeDelegate) GetTypeInfoForSignedFixedPointTypes(
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

        var invokeEncodeUnmanagedElement = _methodInfoElement.MakeGenericMethod(type);
        var parameters = new object[2];

        void encode(Stream driver, object data)
        {
            parameters[0] = driver;
            parameters[1] = data;
            invokeEncodeUnmanagedElement.Invoke(driver, parameters);
        };

        return (message, encode);
    }

#if NET5_0_OR_GREATER
    private static (DatatypeMessage, EncodeDelegate) GetTypeInfoFor16BitFloatingPoint(
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

        var invokeEncodeUnmanagedElement = _methodInfoElement.MakeGenericMethod(type);
        var parameters = new object[2];

        void encode(Stream driver, object data)
        {
            parameters[0] = driver;
            parameters[1] = data;
            invokeEncodeUnmanagedElement.Invoke(null, parameters);
        };

        return (message, encode);
    }
#endif

    private static (DatatypeMessage, EncodeDelegate) GetTypeInfoFor32BitFloatingPoint(
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

        var invokeEncodeUnmanagedElement = _methodInfoElement.MakeGenericMethod(type);
        var parameters = new object[2];

        void encode(Stream driver, object data)
        {
            parameters[0] = driver;
            parameters[1] = data;
            invokeEncodeUnmanagedElement.Invoke(null, parameters);
        };

        return (message, encode);
    }

    private static (DatatypeMessage, EncodeDelegate) GetTypeInfoFor64BitFloatingPoint(
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

        var invokeEncodeUnmanagedElement = _methodInfoElement.MakeGenericMethod(type);
        var parameters = new object[2];

        void encode(Stream driver, object data)
        {
            parameters[0] = driver;
            parameters[1] = data;
            invokeEncodeUnmanagedElement.Invoke(null, parameters);
        };

        return (message, encode);
    }

    private static (DatatypeMessage, EncodeDelegate) GetTypeInfoFor128BitFloatingPoint(
        Type type,
        ByteOrder endianness)
    {
        var message = new DatatypeMessage(

            sizeof(decimal),

            new FloatingPointBitFieldDescription(
                ByteOrder: endianness,
                PaddingTypeLow: default,
                PaddingTypeHigh: default,
                PaddingTypeInternal: default,
                MantissaNormalization: MantissaNormalization.MsbIsNotStoredButImplied,
                SignLocation: 127
            ),

            // https://en.wikipedia.org/wiki/IEEE_754#Basic_and_interchange_formats
            new FloatingPointPropertyDescription[] {
                new(BitOffset: 0,
                    BitPrecision: 128,
                    ExponentLocation: 112,
                    ExponentSize: 15,
                    MantissaLocation: 0,
                    MantissaSize: 112,
                    ExponentBias: 16383
                )
            }
        )
        {
            Version = DATATYPE_MESSAGE_VERSION,
            Class = DatatypeMessageClass.FloatingPoint
        };

        var invokeEncodeUnmanagedElement = _methodInfoElement.MakeGenericMethod(type);
        var parameters = new object[2];

        void encode(Stream driver, object data)
        {
            parameters[0] = driver;
            parameters[1] = data;
            invokeEncodeUnmanagedElement.Invoke(null, parameters);
        };

        return (message, encode);
    }

    private static (DatatypeMessage, ulong[], EncodeDelegate) GetTypeInfoForTopLevelDictionary(
        WriteContext context,
        Type type,
        IDictionary topLevelData)
    {
        var (valueMessage, valueEncode) = InternalCreate(context, type.GenericTypeArguments[1]);
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

        var dimensions = new ulong[] { 1 };

        void encode(Stream driver, object data)
        {
            var dataAsDictionary = (IDictionary)data;

            foreach (var value in topLevelData.Values)
            {
                valueEncode(driver, value);
            }
        }

        return (message, dimensions, encode);
    }

    private static (DatatypeMessage, ulong[], EncodeDelegate) GetTypeInfoForTopLevelEnumerable(
        WriteContext context,
        Type type,
        IEnumerable topLevelData,
        bool isArray = false)
    {
        var elementType = type.IsArray
            ? type.GetElementType()!
            : type.GenericTypeArguments[0];

        var (message, elementEncode) = InternalCreate(context, elementType);

        ulong[] dimensions;

        if (isArray)
        {
            var arrayData = (Array)topLevelData;

            dimensions = Enumerable
                .Range(0, arrayData.Rank)
                .Select(dimension => (ulong)arrayData.GetLongLength(dimension))
                .ToArray();
        }

        else
        {
            dimensions = new ulong[]
            {
                (ulong)WriteUtils.GetEnumerableLength(topLevelData)
            };
        }

        void encode(Stream driver, object data)
        {
            var enumerable = (IEnumerable)data;
            var enumerator = enumerable.GetEnumerator();

            while (enumerator.MoveNext())
            {
                var currentElement = enumerator.Current;
                elementEncode(driver, currentElement);
            }
        }

        return (message, dimensions, encode);
    }

    private static (DatatypeMessage, ulong[], EncodeDelegate) GetTypeInfoForTopLevelUnmanagedArray(
        WriteContext context,
        Type elementType,
        Array topLevelData)
    {
        var (message, _) = InternalCreate(context, elementType);

        var dimensions = Enumerable
            .Range(0, topLevelData.Rank)
            .Select(dimension => (ulong)topLevelData.GetLongLength(dimension))
            .ToArray();

        void encode(Stream driver, object data)
        {
#if NET6_0_OR_GREATER
            var span = MemoryMarshal.CreateSpan(
                reference: ref MemoryMarshal.GetArrayDataReference(topLevelData), 
                length: topLevelData.Length * (int)message.Size);

            driver.Write(span);
#else
            if (topLevelData.Rank != 1)
                throw new Exception("Multi-dimensions arrays are only supported on .NET 6+.");

            else
                InvokeEncodeUnmanagedArray(elementType, driver, data);
#endif
        }

        return (message, dimensions, encode);
    }

    private static (DatatypeMessage, ulong[], EncodeDelegate) GetTypeInfoForTopLevelMemory(
        WriteContext context,
        Type elementType,
        object topLevelData)
    {
        var (message, elementEncode) = InternalCreate(context, elementType);

        var dimensions = new ulong[] {
            (ulong)WriteUtils.InvokeGetMemoryLengthGeneric(elementType, topLevelData)
        };

        void encode(Stream driver, object data)
            => InvokeEncodeMemory(
                elementType, 
                driver, 
                data, 
                elementEncode);

        return (message, dimensions, encode);
    }

    private static (DatatypeMessage, ulong[], EncodeDelegate) GetTypeInfoForTopLevelUnmanagedMemory(
        WriteContext context,
        Type elementType,
        object topLevelData)
    {
        var (message, _) = InternalCreate(context, elementType);

        var dimensions = new ulong[] {
            (ulong)WriteUtils.InvokeGetMemoryLengthGeneric(elementType, topLevelData)
        };

        void encode(Stream driver, object data) 
            => InvokeEncodeUnmanagedMemory(elementType, driver, data);

        return (message, dimensions, encode);
    }

    // Unmanaged element
    private static readonly MethodInfo _methodInfoElement = typeof(DatatypeMessage)
        .GetMethod(nameof(EncodeUnmanagedElement), BindingFlags.NonPublic | BindingFlags.Static)!;

    private static void EncodeUnmanagedElement<T>(Stream driver, object data) where T : unmanaged
    {
        Span<T> source = stackalloc T[] { (T)data };

        driver.Write(MemoryMarshal.AsBytes(source));
    }

    // Unmanaged array
    private static readonly MethodInfo _methodInfoUnmanagedArray = typeof(DatatypeMessage)
        .GetMethod(nameof(EncodeUnmanagedArray), BindingFlags.NonPublic | BindingFlags.Static)!;

    // TODO: cache the generic method for cases where there are large amount of datasets/attributes with different datatype
    private static void InvokeEncodeUnmanagedArray(Type type, Stream driver, object data)
    {
        var genericMethod = _methodInfoUnmanagedArray.MakeGenericMethod(type);
        genericMethod.Invoke(null, new object[] { driver, data });
    }

    private static void EncodeUnmanagedArray<T>(Stream driver, object data) where T : unmanaged
    {
        driver.Write(MemoryMarshal.AsBytes<T>((T[])data));
    }

    // Unmanaged Memory
    private static readonly MethodInfo _methodInfoUnmanagedMemory = typeof(DatatypeMessage)
        .GetMethod(nameof(EncodeUnmanagedMemory), BindingFlags.NonPublic | BindingFlags.Static)!;

    // TODO: cache the generic method for cases where there are large amount of datasets/attributes with different datatype
    private static void InvokeEncodeUnmanagedMemory(Type type, Stream driver, object data)
    {
        var genericMethod = _methodInfoUnmanagedMemory.MakeGenericMethod(type);
        genericMethod.Invoke(null, new object[] { driver, data });
    }

    private static void EncodeUnmanagedMemory<T>(Stream driver, object data) where T : unmanaged
    {
        driver.Write(MemoryMarshal.AsBytes(((Memory<T>)data).Span));
    }

    // Memory
    private static readonly MethodInfo _methodInfoMemory = typeof(DatatypeMessage)
        .GetMethod(nameof(EncodeMemory), BindingFlags.NonPublic | BindingFlags.Static)!;

    // TODO: cache the generic method for cases where there are large amount of datasets/attributes with different datatype
    private static void InvokeEncodeMemory(
        Type type, 
        Stream driver, 
        object data, 
        EncodeDelegate elementEncode)
    {
        var genericMethod = _methodInfoMemory.MakeGenericMethod(type);

        genericMethod.Invoke(null, new object[] 
        {
            driver, 
            data, 
            elementEncode
        });
    }

    private static void EncodeMemory<T>(
        Stream driver, 
        object data, 
        EncodeDelegate elementEncode)
    {
        var span = ((Memory<T>)data).Span;

        for (int i = 0; i < span.Length; i++)
        {
            elementEncode(driver, span[i]!);
        }
    }

    public override void Encode(BinaryWriter driver)
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
}