using System.Collections;
using System.Reflection;
using System.Runtime.InteropServices;

namespace PureHDF.VOL.Native;

// TODO: use this for generic structs https://github.com/SergeyTeplyakov/ObjectLayoutInspector?

internal partial record class DatatypeMessage : Message
{
    private const int DATATYPE_MESSAGE_VERSION = 3;

    // reference size                = GHEAP address + GHEAP index
    private const int REFERENCE_SIZE = sizeof(ulong) + sizeof(uint);

    // variable length entry size           length
    private const int VLEN_REFERENCE_SIZE = sizeof(uint) + REFERENCE_SIZE;

    private static DatatypeMessage Create(
        Dictionary<Type, DatatypeMessage> cache,
        Type type,
        object? topLevelData = default)
    {
        if (cache.TryGetValue(type, out var cachedMessage))
            return cachedMessage;

        var isTopLevel = topLevelData is not null;

        var endianness = BitConverter.IsLittleEndian 
            ? ByteOrder.LittleEndian 
            : ByteOrder.BigEndian;

        var newMessage = type switch
        {
            /* dictionary */
            Type when typeof(IDictionary).IsAssignableFrom(type) && 
                        type.GenericTypeArguments[0] == typeof(string) 
                => isTopLevel

                    /* compound */
                    ? new DatatypeMessage(

                        (uint)(Create(cache, type.GenericTypeArguments[1]).Size * ((IDictionary)topLevelData!).Count),

                        new CompoundBitFieldDescription(
                            MemberCount: (ushort)((IDictionary)topLevelData!).Count
                        ),

                        // TODO: this is wrong
                        GetTypeInfoForObject(cache, type).Properties
                    )
                    {
                        Version = DATATYPE_MESSAGE_VERSION,
                        Class = DatatypeMessageClass.Compound
                    }

                    /* variable-length list of key-value pairs */
                    : GetTypeInfoForVariableLengthSequence(
                        baseType: Create(cache, typeof(KeyValuePair<,>).MakeGenericType(type.GenericTypeArguments))
                    ),

            /* array */
            Type when type.IsArray && type.GetArrayRank() == 1 && type.GetElementType() is not null
                => isTopLevel

                    /* array */
                    ? Create(cache, type.GetElementType()!)

                    /* variable-length list of elements */
                    : GetTypeInfoForVariableLengthSequence(
                        baseType: Create(cache, type.GetElementType()!)
                    ),

            /* generic IEnumerable */
            Type when typeof(IEnumerable).IsAssignableFrom(type) && type.IsGenericType
                => isTopLevel

                    /* array */
                    ? Create(cache, type.GenericTypeArguments[0])

                    /* variable-length list of elements */
                    : GetTypeInfoForVariableLengthSequence(
                        baseType: Create(cache, type.GenericTypeArguments[0])
                    ),

            /* string */
            Type when type == typeof(string)
                => new DatatypeMessage(

                    REFERENCE_SIZE,

                    new VariableLengthBitFieldDescription(
                        Type: InternalVariableLengthType.String,
                        PaddingType: PaddingType.NullTerminate,
                        Encoding: CharacterSetEncoding.UTF8
                    ),

                    new VariableLengthPropertyDescription[] { 
                        new (
                            BaseType: Create(cache, typeof(byte))
                        )
                    }
                )
                {
                    Version = DATATYPE_MESSAGE_VERSION,
                    Class = DatatypeMessageClass.VariableLength
                },

            /* remaining reference types */
            Type when ReadUtils.IsReferenceOrContainsReferences(type) 
                => GetTypeInfoForObject(cache, type),

            /* non blittable */
            Type when type == typeof(bool) 
                => Create(cache, typeof(byte)),

            /* enumeration */
            Type when type.IsEnum 
                => GetTypeInfoForEnum(cache, type),

            /* unsigned fixed-point types */
            Type when 
                type == typeof(byte) || 
                type == typeof(ushort) || 
                type == typeof(uint) || 
                type == typeof(ulong) 
                => new DatatypeMessage(

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
                },

            /* signed fixed-point types */
            Type when
                type == typeof(sbyte) || 
                type == typeof(short) || 
                type == typeof(int) || 
                type == typeof(long) 
                => new DatatypeMessage(

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
                },

            /* 32 bit floating-point */
            Type when type == typeof(float) 
                => new DatatypeMessage(

                    (uint)Marshal.SizeOf(type),
                    
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
                },

            /* 64 bit floating-point */
            Type when type == typeof(double) 
                => new DatatypeMessage(

                    (uint)Marshal.SizeOf(type),
                    
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
                },

            /* remaining non-generic value types */
            Type when type.IsValueType && !type.IsGenericType 
                => GetTypeInfoForStruct(cache, type),

            /* remaining generic value types */
            Type when type.IsValueType 
                => GetTypeInfoForObject(cache, type, useFields: true),

            _ => throw new NotSupportedException($"The data type '{type}' is not supported."),
        };

        // do not cache IEnumerable types when we are in top-level mode
        if (isTopLevel && typeof(IEnumerable).IsAssignableFrom(type))
        {
            //
        }

        else
        {
            cache[type] = newMessage;
        }
        
        return newMessage;
    }

    private static DatatypeMessage GetTypeInfoForEnum(
        Dictionary<Type, DatatypeMessage> cache, 
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

        var properties = new EnumerationPropertyDescription(
            BaseType: Create(cache, underlyingType),
            Names: Enum.GetNames(type),
            Values: values
        );

        return new DatatypeMessage(
            Create(cache, Enum.GetUnderlyingType(type)).Size,

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
    }

    private static DatatypeMessage GetTypeInfoForStruct(
        Dictionary<Type, DatatypeMessage> cache,
        Type type)
    {
        var fieldInfos = type.GetFields(BindingFlags.Public | BindingFlags.Instance);
        var properties = new CompoundPropertyDescription[fieldInfos.Length];

        for (int i = 0; i < fieldInfos.Length; i++)
        {
            var fieldInfo = fieldInfos[i];
            var underlyingType = fieldInfo.FieldType;

            properties[i] = new CompoundPropertyDescription(
                Name: fieldInfo.Name,
                MemberByteOffset: (ulong)Marshal.OffsetOf(type, fieldInfo.Name),
                MemberTypeMessage: Create(cache, underlyingType)
            );
        }

        var bitfield = new CompoundBitFieldDescription(
            MemberCount: (ushort)fieldInfos.Length
        );

        return new DatatypeMessage(
            (uint)Marshal.SizeOf(type),            
            bitfield,
            properties
        )
        {
            Version = DATATYPE_MESSAGE_VERSION,
            Class = DatatypeMessageClass.Compound
        };
    }

    private static DatatypeMessage GetTypeInfoForObject(
        Dictionary<Type, DatatypeMessage> cache,
        Type type, 
        bool useFields = false)
    {
        DatatypeBitFieldDescription bitfield;
        DatatypePropertyDescription[] properties;

        var count = default(ushort);
        var offset = 0U;

        if (useFields)
        {
            var fieldInfos = type
                .GetFields(BindingFlags.Public | BindingFlags.Instance);

            properties = new CompoundPropertyDescription[fieldInfos.Length];

            for (int i = 0; i < fieldInfos.Length; i++)
            {
                var fieldInfo = fieldInfos[i];
                var underlyingType = fieldInfo.FieldType;
                var dataTypeMessage = Create(cache, underlyingType);

                properties[i] = new CompoundPropertyDescription(
                    Name: fieldInfo.Name,
                    MemberByteOffset: offset,
                    MemberTypeMessage: dataTypeMessage
                );

                count += 1;
                offset += dataTypeMessage.Size;
            }

            bitfield = new CompoundBitFieldDescription(
                MemberCount: count
            );
        }

        else
        {

            var propertyInfos = type
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(propertyInfo => propertyInfo.CanRead)
                .ToArray();

            properties = new CompoundPropertyDescription[propertyInfos.Length];

            for (int i = 0; i < propertyInfos.Length; i++)
            {
                var propertyInfo = propertyInfos[i];
                var underlyingType = propertyInfo.PropertyType;
                var dataTypeMessage = Create(cache,  underlyingType);

                properties[i] = new CompoundPropertyDescription(
                    Name: propertyInfo.Name,
                    MemberByteOffset: offset,
                    MemberTypeMessage: dataTypeMessage
                );

                count += 1;
                offset += dataTypeMessage.Size;
            }

            bitfield = new CompoundBitFieldDescription(
                MemberCount: count
            );
        }

        return new DatatypeMessage(
            offset,            
            bitfield, 
            properties
        )
        {
            Version = DATATYPE_MESSAGE_VERSION,
            Class = DatatypeMessageClass.Compound
        };
    }

    private static DatatypeMessage GetTypeInfoForVariableLengthSequence(
        DatatypeMessage baseType)
    {
        return new DatatypeMessage(
            VLEN_REFERENCE_SIZE,

            new VariableLengthBitFieldDescription(
                Type: InternalVariableLengthType.Sequence,
                PaddingType: default,
                Encoding: default
            ),

            new VariableLengthPropertyDescription[] {
                new (
                    BaseType: baseType
                )
            }
        )
        {
            Version = DATATYPE_MESSAGE_VERSION,
            Class = DatatypeMessageClass.VariableLength
        };
    }

    public static Memory<byte> EncodeData(Type type, Memory<byte> result, object? data = default)
    {
        var isTopLevel = data is not null;

        return type switch
        {
            // /* dictionary */
            // Type when typeof(IDictionary).IsAssignableFrom(type) && 
            //             type.GenericTypeArguments[0] == typeof(string)
            //     => isTopLevel

            //         /* compound */
            //         ? throw new NotImplementedException()

            //         /* variable-length list of key-value pairs */
            //         : new CastMemoryManager<VariableLengthElement, byte>(new VariableLengthElement[] {
            //             new(
            //                 Length:(ushort)((IDictionary)data!).Count,
            //                 HeapId: GetGlobalHeapId())
            //         }).Memory,

            /* array */
            Type when type.IsArray && type.GetArrayRank() == 1 && type.GetElementType() is not null
                => isTopLevel

                    /* array */
                    ? throw new NotImplementedException()

                    /* variable-length list of elements */
                    : throw new NotImplementedException(),

            /* generic IEnumerable */
            Type when typeof(IEnumerable).IsAssignableFrom(type) && type.IsGenericType
                => isTopLevel

                    /* array */
                    ? EncodeEnumerable(type, result, data)

                    /* variable-length list of elements */
                    : throw new NotImplementedException(),

            // /* string */
            // Type when type == typeof(string)
            //     => new CastMemoryManager<GlobalHeapId, byte>(new VariableLengthElement[] {
            //         GetGlobalHeapId()
            //     }).Memory,

            /* remaining reference types */
            Type when ReadUtils.IsReferenceOrContainsReferences(type) 
                => EncodeObject(type, result, data),

            /* non blittable */
            Type when type == typeof(bool) 
                => new byte[] { ((bool)data!) ? (byte)1 : (byte)0 },

            /* enumeration */
            Type when type.IsEnum 
                => InvokeEncodeUnmanaged(Enum.GetUnderlyingType(type), result, data),

            /* remaining non-generic value types */
            Type when type.IsValueType && !type.IsGenericType 
                => InvokeEncodeUnmanaged(type, result, data),

            /* remaining generic value types */
            Type when type.IsValueType
                => EncodeObject(type, result, data, useFields: true),

            _ => throw new NotSupportedException($"The data type '{type}' is not supported."),
        };
    }

    private static readonly MethodInfo _methodInfo = typeof(DatatypeMessage)
        .GetMethod(nameof(EncodeUnmanaged), BindingFlags.NonPublic | BindingFlags.Static)!;

    private static Memory<byte> EncodeEnumerable(Type type, Memory<byte> result, IEnumerable data)
    {
        var current = result;
        var elementType = type.GenericTypeArguments[0];
        var encodeMethod = GetEncodeMethod(elementType);

        foreach (var element in data)
        {
            encodeMethod(element, result);
            current = current[elementSize..];
        }

        return result;
    }

    private static Memory<byte> EncodeObject(Type type, Memory<byte> result, object data, bool useFields = false)
    {
        var current = result;

        if (useFields)
        {
            var fieldInfos = type
                .GetFields(BindingFlags.Public | BindingFlags.Instance);

            foreach (var fieldInfo in fieldInfos)
            {
                var underlyingType = fieldInfo.FieldType;
                var value = fieldInfo.GetValue(data);

                EncodeData(underlyingType, current, value);

                var size = Create(underlyingType);
                current = current[size..];
            }
        }

        else
        {
            var propertyInfos = type
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(propertyInfo => propertyInfo.CanRead);

            foreach (var propertyInfo in propertyInfos)
            {
                var underlyingType = propertyInfo.PropertyType;
                var value = propertyInfo.GetValue(data);

                EncodeData(underlyingType, current, value);

                var size = Create(underlyingType);
                current = current[size..];
            }
        }

        return result;
    }

    private static Memory<byte> InvokeEncodeUnmanaged(Type type, Memory<byte> result, object data)
    {
        var genericMethod = _methodInfo.MakeGenericMethod(type);
        return (Memory<byte>)genericMethod.Invoke(null, new object[] { result, data })!;
    }

    private static Memory<byte> EncodeUnmanaged<T>(Memory<byte> result, object data) where T : unmanaged
    {
        var type = typeof(T);

        if (type.IsArray && type.GetArrayRank() == 1 && type.GetElementType() is not null)
        {
            return new CastMemoryManager<T, byte>((T[])data).Memory;
        }

        else if (typeof(IEnumerable).IsAssignableFrom(type) && type.IsGenericType)
        {
            new CastMemoryManager<T, byte>(((IEnumerable<T>)data).ToArray())
                .Memory
                .CopyTo(result);

            return result;
        }

        else 
        {
            Span<T> source = stackalloc T[] { (T)data };

            MemoryMarshal
                .AsBytes(source)
                .CopyTo(result.Span);

            return result;
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