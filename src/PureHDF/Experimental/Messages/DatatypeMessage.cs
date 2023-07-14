using System.Collections;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using PureHDF.Experimental;

namespace PureHDF.VOL.Native;

// TODO: use this for generic structs https://github.com/SergeyTeplyakov/ObjectLayoutInspector?

internal delegate void EncodeDelegate(ref Memory<byte> target, object data);

internal partial record class DatatypeMessage : Message
{
    private const int DATATYPE_MESSAGE_VERSION = 3;

    // reference size                = GHEAP address + GHEAP index
    private const int REFERENCE_SIZE = sizeof(ulong) + sizeof(uint);

    // variable length entry size           length
    private const int VLEN_REFERENCE_SIZE = sizeof(uint) + REFERENCE_SIZE;

    public static (DatatypeMessage, EncodeDelegate) Create(
        WriteContext context,
        Type type,
        object topLevelData
    )
    {
        return type switch
        {
            /* dictionary */
            Type when typeof(IDictionary).IsAssignableFrom(type) && type.GenericTypeArguments[0] == typeof(string)
                => GetTypeInfoForTopLevelDictionary(context, type, topLevelData),

            /* array */
            Type when type.IsArray && type.GetArrayRank() == 1 && type.GetElementType() is not null
                => ReadUtils.IsReferenceOrContainsReferences(type.GetElementType()!)
                    ? GetTypeInfoForEnumerable(context, type)
                    : GetTypeInfoForArray(context, type.GetElementType()!),

            /* Memory<T> */
            Type when type.IsGenericType && typeof(Memory<>).Equals(type.GetGenericTypeDefinition())
                => ReadUtils.IsReferenceOrContainsReferences(type.GenericTypeArguments[0])
                    ? GetTypeInfoForEnumerable(context, type)
                    : GetTypeInfoForMemory(context, type.GenericTypeArguments[0]),

            /* generic IEnumerable */
            Type when typeof(IEnumerable).IsAssignableFrom(type) && type.IsGenericType
                => GetTypeInfoForEnumerable(context, type),

            _ => InternalCreate(context, type)
        };
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
            /* dictionary */
            Type when typeof(IDictionary).IsAssignableFrom(type) &&
                        type.GenericTypeArguments[0] == typeof(string)
                => GetTypeInfoForVariableLengthSequence(context, typeof(KeyValuePair<,>)
                    .MakeGenericType(type.GenericTypeArguments)),

            /* array */
            Type when type.IsArray && type.GetArrayRank() == 1 && type.GetElementType() is not null
                => GetTypeInfoForVariableLengthSequence(context, type.GetElementType()!),

            /* generic IEnumerable */
            Type when typeof(IEnumerable).IsAssignableFrom(type) && type.IsGenericType
                => GetTypeInfoForVariableLengthSequence(context, type.GenericTypeArguments[0]),

            /* string */
            Type when type == typeof(string)
                => GetTypeInfoForVariableLengthString(context),

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

            /* 32 bit floating-point */
            Type when type == typeof(float)
                => GetTypeInfoFor32BitFloatingPoint(type, endianness),

            /* 64 bit floating-point */
            Type when type == typeof(double)
                => GetTypeInfoFor64BitFloatingPoint(type, endianness),

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

        static void encode(ref Memory<byte> target, object data)
            => target.Span[0] = ((bool)data) ? (byte)1 : (byte)0;

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

        void encode(ref Memory<byte> target, object data)
            => InvokeEncodeUnmanagedElement(type, target, data);

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
                    Name: fieldInfo.Name,
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
                    Name: propertyInfo.Name,
                    MemberByteOffset: offset,
                    MemberTypeMessage: propertyMessage
                );

                offset += propertyMessage.Size;
            }
        }

        void encode(ref Memory<byte> target, object data)
        {
            var remaining = target;

            // fields
            for (int i = 0; i < fieldEncodes.Length; i++)
            {
                var memberEncode = fieldEncodes[i];
                var typeSize = (int)properties[i].MemberTypeMessage.Size;
                var fieldInfo = fieldInfos[i];

                memberEncode(ref remaining, fieldInfo.GetValue(data)!);

                remaining = remaining[typeSize..];
            }

            // properties
            for (int i = 0; i < propertyEncodes.Length; i++)
            {
                var memberEncode = propertyEncodes[i];
                var typeSize = (int)properties[i].MemberTypeMessage.Size;
                var propertyInfo = propertyInfos[i];

                memberEncode(ref remaining, propertyInfo.GetValue(data)!);

                remaining = remaining[typeSize..];
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

        static void encode(ref Memory<byte> target, object data)
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

            targetSpan = targetSpan[sizeof(int)..];

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
        WriteContext context)
    {
        var (baseMessage, baseEncode) = InternalCreate(context, typeof(byte));

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

        static void encode(ref Memory<byte> target, object data)
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

            targetSpan = targetSpan[sizeof(int)..];

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

        void encode(ref Memory<byte> target, object data)
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

        void encode(ref Memory<byte> target, object data)
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

        void encode(ref Memory<byte> target, object data)
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

        void encode(ref Memory<byte> target, object data)
            => InvokeEncodeUnmanagedElement(type, target, data);

        return (message, encode);
    }

    private static (DatatypeMessage, EncodeDelegate) GetTypeInfoForTopLevelDictionary(
        WriteContext context,
        Type type,
        object topLevelData)
    {
        var dictionary = (IDictionary)topLevelData;

        var (valueMessage, valueEncode) = InternalCreate(context, type.GenericTypeArguments[1]);
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

        void encode(ref Memory<byte> target, object data)
        {
            var localTarget = target;
            var dataAsDictionary = (IDictionary)data;

            foreach (var value in dictionary.Values)
            {
                valueEncode(ref localTarget, value);

                localTarget = localTarget[(int)memberSize..];
            }
        }

        return (message, encode);
    }

    private static (DatatypeMessage, EncodeDelegate) GetTypeInfoForEnumerable(
        WriteContext context,
        Type type)
    {
        var elementType = type.IsArray
            ? type.GetElementType()!
            : type.GenericTypeArguments[0];

        var (message, elementEncode) = InternalCreate(context, elementType);

        void encode(ref Memory<byte> target, object data)
        {
            var enumerable = (IEnumerable)data;
            var enumerator = enumerable.GetEnumerator();
            var remaining = target;

            while (enumerator.MoveNext())
            {
                var currentElement = enumerator.Current;

                elementEncode(ref remaining, currentElement);

                remaining = remaining[(int)message.Size..];
            }
        }

        return (message, encode);
    }

    private static (DatatypeMessage, EncodeDelegate) GetTypeInfoForArray(
        WriteContext context,
        Type elementType)
    {
        var (message, _) = InternalCreate(context, elementType);

        void encode(ref Memory<byte> target, object data)
        {
            target = InvokeEncodeUnmanagedArray(elementType, target, data);
        }

        return (message, encode);
    }

    private static (DatatypeMessage, EncodeDelegate) GetTypeInfoForMemory(
        WriteContext context,
        Type elementType)
    {
        var (message, _) = InternalCreate(context, elementType);

        void encode(ref Memory<byte> target, object data)
        {
            target = InvokeEncodeUnmanagedMemory(elementType, target, data);
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

    private static readonly MethodInfo _methodInfoMemory = typeof(DatatypeMessage)
        .GetMethod(nameof(EncodeUnmanagedMemory), BindingFlags.NonPublic | BindingFlags.Static)!;

    private static Memory<byte> InvokeEncodeUnmanagedMemory(Type type, Memory<byte> result, object data)
    {
        var genericMethod = _methodInfoMemory.MakeGenericMethod(type);
        return (Memory<byte>)genericMethod.Invoke(null, new object[] { result, data })!;
    }

    private static Memory<byte> EncodeUnmanagedMemory<T>(Memory<byte> _, object data) where T : unmanaged
    {
        return new CastMemoryManager<T, byte>((Memory<T>)data).Memory;
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