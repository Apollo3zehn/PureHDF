using System.Collections;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace PureHDF.VOL.Native;

// TODO: use this for generic structs https://github.com/SergeyTeplyakov/ObjectLayoutInspector?

internal delegate void EncodeDelegate(Memory<byte> target, object data);

internal partial record class DatatypeMessage : Message
{
    private const int DATATYPE_MESSAGE_VERSION = 3;

    // reference size                = GHEAP address + GHEAP index
    private const int REFERENCE_SIZE = sizeof(ulong) + sizeof(uint);

    // variable length entry size           length
    private const int VLEN_REFERENCE_SIZE = sizeof(uint) + REFERENCE_SIZE;

    public static (DatatypeMessage, EncodeDelegate) Create(
        Dictionary<Type, (DatatypeMessage, EncodeDelegate)> cache,
        Type type,
        object topLevelData
    )
    {
        return type switch
        {
            /* dictionary */
            Type when typeof(IDictionary).IsAssignableFrom(type) && type.GenericTypeArguments[0] == typeof(string)
                => GetTypeInfoForTopLevelDictionary(cache, type, topLevelData),

            /* array */
            Type when type.IsArray && type.GetArrayRank() == 1 && type.GetElementType() is not null
                => ReadUtils.IsReferenceOrContainsReferences(type)
                    ? GetTypeInfoForEnumerable(cache, type)
                    : GetTypeInfoForArray(cache, type),

            /* generic IEnumerable */
            Type when typeof(IEnumerable).IsAssignableFrom(type) && type.IsGenericType
                => GetTypeInfoForEnumerable(cache, type),

            _ => InternalCreate(cache, type)
        };
    }

    private static (DatatypeMessage, EncodeDelegate) InternalCreate(
        Dictionary<Type, (DatatypeMessage, EncodeDelegate)> cache,
        Type type)
    {
        if (cache.TryGetValue(type, out var cachedMessage))
            return cachedMessage;

        var endianness = BitConverter.IsLittleEndian
            ? ByteOrder.LittleEndian
            : ByteOrder.BigEndian;

        (DatatypeMessage newMessage, EncodeDelegate encode) = type switch
        {
            /* dictionary */
            Type when typeof(IDictionary).IsAssignableFrom(type) &&
                        type.GenericTypeArguments[0] == typeof(string)
                => GetTypeInfoForVariableLengthSequence(cache, typeof(KeyValuePair<,>).MakeGenericType(type.GenericTypeArguments)),

            /* array */
            Type when type.IsArray && type.GetArrayRank() == 1 && type.GetElementType() is not null
                => GetTypeInfoForVariableLengthSequence(cache, type.GetElementType()!),

            /* generic IEnumerable */
            Type when typeof(IEnumerable).IsAssignableFrom(type) && type.IsGenericType
                => GetTypeInfoForVariableLengthSequence(cache, type.GenericTypeArguments[0]),

            /* string */
            Type when type == typeof(string)
                => GetTypeInfoForVariableLengthString(cache),

            /* remaining reference types */
            Type when ReadUtils.IsReferenceOrContainsReferences(type)
                => GetTypeInfoForObject(cache, type),

            /* non blittable */
            Type when type == typeof(bool)
                => GetTypeInfoForBool(cache),

            /* enumeration */
            Type when type.IsEnum
                => GetTypeInfoForEnum(cache, type),

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

            /* 32 bit floating-point */
            Type when type == typeof(float)
                => GetTypeInfoFor32BitFloatingPoint(type, endianness),

            /* 64 bit floating-point */
            Type when type == typeof(double)
                => GetTypeInfoFor64BitFloatingPoint(type, endianness),

            /* remaining non-generic value types */
            Type when type.IsValueType && !type.IsGenericType
                => GetTypeInfoForStruct(cache, type),

            /* remaining generic value types */
            Type when type.IsValueType
                => GetTypeInfoForObject(cache, type, useFields: true),

            _ => throw new NotSupportedException($"The data type '{type}' is not supported."),
        };

        cache[type] = (newMessage, encode);
        return (newMessage, encode);
    }

    private static (DatatypeMessage, EncodeDelegate) GetTypeInfoForBool(
        Dictionary<Type, (DatatypeMessage, EncodeDelegate)> cache
    )
    {
        var (baseMessage, _) = InternalCreate(cache, typeof(byte));

        static void encode(Memory<byte> target, object data)
            => target.Span[0] = ((bool)data) ? (byte)1 : (byte)0;

        return (baseMessage, encode);
    }

    private static (DatatypeMessage, EncodeDelegate) GetTypeInfoForEnum(
        Dictionary<Type, (DatatypeMessage, EncodeDelegate)> cache,
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
            Type t when t == typeof(byte) => enumObjects.Select(enumValue => BitConverter.GetBytes((byte)enumValue)),
            Type t when t == typeof(sbyte) => enumObjects.Select(enumValue => BitConverter.GetBytes((sbyte)enumValue)),
            Type t when t == typeof(ushort) => enumObjects.Select(enumValue => BitConverter.GetBytes((ushort)enumValue)),
            Type t when t == typeof(short) => enumObjects.Select(enumValue => BitConverter.GetBytes((short)enumValue)),
            Type t when t == typeof(uint) => enumObjects.Select(enumValue => BitConverter.GetBytes((uint)enumValue)),
            Type t when t == typeof(int) => enumObjects.Select(enumValue => BitConverter.GetBytes((int)enumValue)),
            Type t when t == typeof(ulong) => enumObjects.Select(enumValue => BitConverter.GetBytes((ulong)enumValue)),
            Type t when t == typeof(long) => enumObjects.Select(enumValue => BitConverter.GetBytes((long)enumValue)),
            _ => throw new Exception($"The enum type {underlyingType} is not supported.")
        }).ToArray();

        var (baseMessage, baseEncode) = InternalCreate(cache, Enum.GetUnderlyingType(type));

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

    private static (DatatypeMessage, EncodeDelegate) GetTypeInfoForStruct(
        Dictionary<Type, (DatatypeMessage, EncodeDelegate)> cache,
        Type type)
    {
        var fieldInfos = type.GetFields(BindingFlags.Public | BindingFlags.Instance);
        var properties = new CompoundPropertyDescription[fieldInfos.Length];

        for (int i = 0; i < fieldInfos.Length; i++)
        {
            var fieldInfo = fieldInfos[i];
            var underlyingType = fieldInfo.FieldType;
            var (fieldMessage, _) = InternalCreate(cache, underlyingType);

            properties[i] = new CompoundPropertyDescription(
                Name: fieldInfo.Name,
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

        void encode(Memory<byte> target, object data)
            => InvokeEncodeUnmanagedElement(type, target, data);

        return (message, encode);
    }

    private static (DatatypeMessage, EncodeDelegate) GetTypeInfoForObject(
        Dictionary<Type, (DatatypeMessage, EncodeDelegate)> cache,
        Type type,
        bool useFields = false)
    {
        CompoundBitFieldDescription bitfield;
        CompoundPropertyDescription[] properties;

        var count = default(ushort);
        var offset = 0U;

        EncodeDelegate encode;

        if (useFields)
        {
            var fieldInfos = type
                .GetFields(BindingFlags.Public | BindingFlags.Instance);

            properties = new CompoundPropertyDescription[fieldInfos.Length];
            var fieldEncodes = new EncodeDelegate[fieldInfos.Length];

            for (int i = 0; i < fieldInfos.Length; i++)
            {
                var fieldInfo = fieldInfos[i];
                var underlyingType = fieldInfo.FieldType;
                var (fieldMessage, fieldEncode) = InternalCreate(cache, underlyingType);

                fieldEncodes[i] = fieldEncode;

                properties[i] = new CompoundPropertyDescription(
                    Name: fieldInfo.Name,
                    MemberByteOffset: offset,
                    MemberTypeMessage: fieldMessage
                );

                count += 1;
                offset += fieldMessage.Size;
            }

            bitfield = new CompoundBitFieldDescription(
                MemberCount: count
            );

            encode = (target, data) =>
            {
                var remaining = target;

                for (int i = 0; i < fieldEncodes.Length; i++)
                {
                    var memberEncode = fieldEncodes[i];
                    var typeSize = (int)properties[i].MemberTypeMessage.Size;
                    var fieldInfo = fieldInfos[i];

                    memberEncode(remaining, fieldInfo.GetValue(data)!);

                    remaining = remaining[typeSize..];
                }
            };
        }

        else
        {
            var propertyInfos = type
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(propertyInfo => propertyInfo.CanRead)
                .ToArray();

            properties = new CompoundPropertyDescription[propertyInfos.Length];
            var propertyEncodes = new EncodeDelegate[propertyInfos.Length];

            for (int i = 0; i < propertyInfos.Length; i++)
            {
                var propertyInfo = propertyInfos[i];
                var underlyingType = propertyInfo.PropertyType;
                var (propertyMessage, propertyEncode) = InternalCreate(cache, underlyingType);

                propertyEncodes[i] = propertyEncode;

                properties[i] = new CompoundPropertyDescription(
                    Name: propertyInfo.Name,
                    MemberByteOffset: offset,
                    MemberTypeMessage: propertyMessage
                );

                count += 1;
                offset += propertyMessage.Size;
            }

            bitfield = new CompoundBitFieldDescription(
                MemberCount: count
            );

            encode = (target, data) =>
            {
                var remaining = target;

                for (int i = 0; i < propertyEncodes.Length; i++)
                {
                    var memberEncode = propertyEncodes[i];
                    var typeSize = (int)properties[i].MemberTypeMessage.Size;
                    var propertyInfo = propertyInfos[i];

                    memberEncode(remaining, propertyInfo.GetValue(data)!);

                    remaining = remaining[typeSize..];
                }
            };
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
        Dictionary<Type, (DatatypeMessage, EncodeDelegate)> cache,
        Type baseType)
    {
        var (baseMessage, baseEncode) = InternalCreate(cache, baseType);

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

        static void encode(Memory<byte> target, object data)
        {
            var length = WriteUtils.GetEnumerableLength((IEnumerable)data);
            var globalHeapId = new GlobalHeapId(default, default); // TODO use global heap manager to get real ID
            var targetSpan = target.Span;

            // write length
            Span<int> lengthArray = stackalloc int[1];
            lengthArray[0] = length;

            MemoryMarshal
                .AsBytes(lengthArray)
                .CopyTo(targetSpan);

            target = target[sizeof(int)..];

            // write global heap id
            Span<int> gheapIdArray = stackalloc int[1];
            // gheapIdArray[0] = globalHeapId;

            MemoryMarshal
                .AsBytes(gheapIdArray)
                .CopyTo(targetSpan);
        }

        return (message, encode);
    }

    private static (DatatypeMessage, EncodeDelegate) GetTypeInfoForVariableLengthString(
        Dictionary<Type, (DatatypeMessage, EncodeDelegate)> cache)
    {
        var (baseMessage, baseEncode) = InternalCreate(cache, typeof(byte));

        var message = new DatatypeMessage(

            REFERENCE_SIZE,

            new VariableLengthBitFieldDescription(
                Type: InternalVariableLengthType.String,
                PaddingType: PaddingType.NullTerminate,
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

        static void encode(Memory<byte> target, object data)
        {
            var stringData = (string)data;
            var length = Encoding.UTF8.GetBytes(stringData).Length; // TODO use global heap manager to convert string only once
            var globalHeapId = new GlobalHeapId(default, default); // TODO use global heap manager to get real ID
            var targetSpan = target.Span;

            // write length
            Span<int> lengthArray = stackalloc int[1];
            lengthArray[0] = length;

            MemoryMarshal
                .AsBytes(lengthArray)
                .CopyTo(targetSpan);

            target = target[sizeof(int)..];

            // write global heap id
            Span<int> gheapIdArray = stackalloc int[1];
            // gheapIdArray[0] = globalHeapId;

            MemoryMarshal
                .AsBytes(gheapIdArray)
                .CopyTo(targetSpan);
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

        void encode(Memory<byte> target, object data)
            => InvokeEncodeUnmanagedElement(type, target, data);

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

        void encode(Memory<byte> target, object data)
            => InvokeEncodeUnmanagedElement(type, target, data);

        return (message, encode);
    }

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

        void encode(Memory<byte> target, object data)
            => InvokeEncodeUnmanagedElement(type, target, data);

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

        void encode(Memory<byte> target, object data)
            => InvokeEncodeUnmanagedElement(type, target, data);

        return (message, encode);
    }

    private static (DatatypeMessage, EncodeDelegate) GetTypeInfoForTopLevelDictionary(
        Dictionary<Type, (DatatypeMessage, EncodeDelegate)> cache,
        Type type,
        object topLevelData)
    {
        var dictionary = (IDictionary)topLevelData;

        var (valueMessage, valueEncode) = InternalCreate(cache, type.GenericTypeArguments[1]);
        var memberCount = (ushort)dictionary.Count;
        var memberSize = valueMessage.Size;

        var propertyDescriptions = new CompoundPropertyDescription[memberCount];
        var offset = 0UL;
        var index = 0;

        foreach (DictionaryEntry entry in dictionary)
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

        void encode(Memory<byte> target, object data)
        {
            var dataAsDictionary = (IDictionary)data;

            foreach (var value in dictionary.Values)
            {
                valueEncode(target, value);

                target = target[(int)memberSize..];
            }
        }

        return (message, encode);
    }

    private static (DatatypeMessage, EncodeDelegate) GetTypeInfoForEnumerable(
        Dictionary<Type, (DatatypeMessage, EncodeDelegate)> cache,
        Type type)
    {
        var elementType = type.IsArray
            ? type.GetElementType()!
            : type.GenericTypeArguments[0];

        var (message, elementEncode) = InternalCreate(cache, elementType);

        void encode(Memory<byte> target, object data)
        {
            var enumerable = (IEnumerable)data;
            var enumerator = enumerable.GetEnumerator();
            var remaining = target;

            while (enumerator.MoveNext())
            {
                var currentElement = enumerator.Current;

                elementEncode(remaining, currentElement);

                remaining = remaining[(int)message.Size..];
            }
        }

        return (message, encode);
    }

    private static (DatatypeMessage, EncodeDelegate) GetTypeInfoForArray(
        Dictionary<Type, (DatatypeMessage, EncodeDelegate)> cache,
        Type type)
    {
        var (message, elementEncode) = InternalCreate(cache, type.GetElementType()!);

        void encode(Memory<byte> target, object data)
        {
            InvokeEncodeUnmanagedArray(type, target, data);
        }

        return (message, encode);
    }

    private static readonly MethodInfo _methodInfoElement = typeof(DatatypeMessage)
        .GetMethod(nameof(EncodeUnmanagedElement), BindingFlags.NonPublic | BindingFlags.Static)!;

    private static Memory<byte> InvokeEncodeUnmanagedElement(Type type, Memory<byte> result, object data)
    {
        var genericMethod = _methodInfoElement.MakeGenericMethod(type);
        return (Memory<byte>)genericMethod.Invoke(null, new object[] { result, data })!;
    }

    private static Memory<byte> EncodeUnmanagedElement<T>(Memory<byte> result, object data) where T : unmanaged
    {
        Span<T> source = stackalloc T[] { (T)data };

        MemoryMarshal
            .AsBytes(source)
            .CopyTo(result.Span);

        return result;
    }

    private static readonly MethodInfo _methodInfoArray = typeof(DatatypeMessage)
        .GetMethod(nameof(EncodeUnmanagedArray), BindingFlags.NonPublic | BindingFlags.Static)!;

    private static Memory<byte> InvokeEncodeUnmanagedArray(Type type, Memory<byte> result, object data)
    {
        var genericMethod = _methodInfoArray.MakeGenericMethod(type);
        return (Memory<byte>)genericMethod.Invoke(null, new object[] { result, data })!;
    }

    private static Memory<byte> EncodeUnmanagedArray<T>(Memory<byte> _, object data) where T : unmanaged
    {
        return new CastMemoryManager<T, byte>((T[])data).Memory;
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