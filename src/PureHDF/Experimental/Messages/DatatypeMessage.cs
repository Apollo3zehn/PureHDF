using System.Collections;
using System.Reflection;
using System.Runtime.InteropServices;

namespace PureHDF.VOL.Native;

// TODO: use this for generic structs https://github.com/SergeyTeplyakov/ObjectLayoutInspector?

internal partial record class DatatypeMessage : Message
{
    // reference size                = GHEAP address + GHEAP index
    private const int REFERENCE_SIZE = sizeof(ulong) + sizeof(uint);

    // variable length entry size           length
    private const int VLEN_REFERENCE_SIZE = sizeof(uint) + REFERENCE_SIZE;

    public static DatatypeMessage Create(Type type, object? data = default)
    {
        var isTopLevel = data is not null;
        var (typeSize, bitField) = GetTypeInfo(type, data);

        return new DatatypeMessage(
            Size: (uint)typeSize,
            BitField: bitField,
            Properties: GetPropertyDescriptions(type, typeSize, isTopLevel: isTopLevel)
        )
        {
            Version = 3,
            Class = GetClass(type, isTopLevel: isTopLevel)
        };
    }

    private static (int Size, DatatypeBitFieldDescription BitField) GetTypeInfo(
        Type type,
        object? topLevelData = default)
    {
        var isTopLevel = topLevelData is not null;

        var endianness = BitConverter.IsLittleEndian 
            ? ByteOrder.LittleEndian 
            : ByteOrder.BigEndian;

        return type switch
        {
            /* dictionary */
            Type when typeof(IDictionary).IsAssignableFrom(type) && 
                        type.GenericTypeArguments[0] == typeof(string) 
                => isTopLevel

                    /* compound */
                    ? (
                        GetTypeInfo(type.GenericTypeArguments[1]).Size * ((IDictionary)topLevelData!).Count,

                        new CompoundBitFieldDescription(
                            MemberCount: (ushort)((IDictionary)topLevelData!).Count
                        )
                    )

                    /* variable-length list of key-value pairs */
                    : (
                        VLEN_REFERENCE_SIZE,

                        new VariableLengthBitFieldDescription(
                            Type: InternalVariableLengthType.Sequence,
                            PaddingType: default,
                            Encoding: default
                        )
                    ),

            /* array */
            Type when type.IsArray && type.GetArrayRank() == 1 && type.GetElementType() is not null
                => isTopLevel

                    /* array */
                    ? GetTypeInfo(type.GetElementType()!)

                    /* variable-length list of elements */
                    : (
                        VLEN_REFERENCE_SIZE,

                        new VariableLengthBitFieldDescription(
                            Type: InternalVariableLengthType.Sequence,
                            PaddingType: default,
                            Encoding: default
                        )
                    ),

            /* generic IEnumerable */
            Type when typeof(IEnumerable).IsAssignableFrom(type) && type.IsGenericType
                => isTopLevel

                    /* array */
                    ? GetTypeInfo(type.GenericTypeArguments[0])

                    /* variable-length list of elements */
                    : (
                        VLEN_REFERENCE_SIZE,

                        new VariableLengthBitFieldDescription(
                            Type: InternalVariableLengthType.Sequence,
                            PaddingType: default,
                            Encoding: default
                        )
                    ),

            /* string */
            Type when type == typeof(string)
                => (
                    REFERENCE_SIZE,

                    new VariableLengthBitFieldDescription(
                        Type: InternalVariableLengthType.String,
                        PaddingType: PaddingType.NullTerminate,
                        Encoding: CharacterSetEncoding.UTF8
                    )
                ),

            /* remaining reference types */
            Type when ReadUtils.IsReferenceOrContainsReferences(type) 
                => GetTypeInfoOfObject(type),

            /* non blittable */
            Type when type == typeof(bool) 
                => (
                    1,

                    new FixedPointBitFieldDescription(
                        ByteOrder: endianness,
                        PaddingTypeLow: default,
                        PaddingTypeHigh: default,
                        IsSigned: false
                    )
                ),

            /* enumeration */
            Type when type.IsEnum 
                => (
                    GetTypeInfo(Enum.GetUnderlyingType(type)).Size,

                    new EnumerationBitFieldDescription(
                        MemberCount: (ushort)Enum.GetNames(type).Length
                    )
                ),

            /* unsigned fixed-point types */
            Type when 
                type == typeof(byte) || 
                type == typeof(ushort) || 
                type == typeof(uint) || 
                type == typeof(ulong) 
                => (
                    Marshal.SizeOf(type),
                    
                    new FixedPointBitFieldDescription(
                        ByteOrder: endianness,
                        PaddingTypeLow: default,
                        PaddingTypeHigh: default,
                        IsSigned: false
                    )
                ),

            /* signed fixed-point types */
            Type when
                type == typeof(sbyte) || 
                type == typeof(short) || 
                type == typeof(int) || 
                type == typeof(long) 
                => (
                    Marshal.SizeOf(type),
                    
                    new FixedPointBitFieldDescription(
                        ByteOrder: endianness,
                        PaddingTypeLow: default,
                        PaddingTypeHigh: default,
                        IsSigned: true
                    )
                ),

            /* 32 bit floating-point */
            Type when type == typeof(float) 
                => (
                    Marshal.SizeOf(type),
                    
                    new FloatingPointBitFieldDescription(
                        ByteOrder: endianness,
                        PaddingTypeLow: default,
                        PaddingTypeHigh: default,
                        PaddingTypeInternal: default,
                        MantissaNormalization: MantissaNormalization.MsbIsNotStoredButImplied,
                        SignLocation: 31
                    )
                ),

            /* 64 bit floating-point */
            Type when type == typeof(double) 
                => (
                    Marshal.SizeOf(type),
                    
                    new FloatingPointBitFieldDescription(
                        ByteOrder: endianness,
                        PaddingTypeLow: default,
                        PaddingTypeHigh: default,
                        PaddingTypeInternal: default,
                        MantissaNormalization: MantissaNormalization.MsbIsNotStoredButImplied,
                        SignLocation: 63
                    )
                ),

            /* remaining non-generic value types */
            Type when type.IsValueType && !type.IsGenericType 
                => (
                    Marshal.SizeOf(type),

                    new CompoundBitFieldDescription(
                        MemberCount: (ushort)type.GetFields(BindingFlags.Public | BindingFlags.Instance).Length
                    )
                ),

            /* remaining generic value types */
            Type when type.IsValueType 
                => GetTypeInfoOfObject(type, useFields: true),

            _ => throw new NotSupportedException($"The data type '{type}' is not supported."),
        };
    }

    private static DatatypePropertyDescription[] GetPropertyDescriptions(
        Type type, 
        int typeSize,
        bool isTopLevel = false)
    {
        return type switch
        {
            /* dictionary */
            Type when typeof(IDictionary).IsAssignableFrom(type) && 
                        type.GenericTypeArguments[0] == typeof(string) 
                => isTopLevel

                    /* compound */
                    ? CreateCompoundFromObject(type)

                    /* variable-length list of key-value pairs */
                    : new VariableLengthPropertyDescription[] { 
                        new (
                            BaseType: Create(typeof(KeyValuePair<,>).MakeGenericType(type.GenericTypeArguments))
                        )
                    },

            /* array */
            Type when type.IsArray && type.GetArrayRank() == 1 && type.GetElementType() is not null
                => isTopLevel

                    /* array */
                    ? GetPropertyDescriptions(type.GetElementType()!, typeSize)

                    /* variable-length list of elements */
                    : new VariableLengthPropertyDescription[] { 
                        new (
                            BaseType: Create(type.GetElementType()!)
                        )
                    },

            /* generic IEnumerable */
            Type when typeof(IEnumerable).IsAssignableFrom(type) && type.IsGenericType
                => isTopLevel

                    /* array */
                    ? GetPropertyDescriptions(type.GenericTypeArguments[0], typeSize)

                    /* variable-length list of elements */
                    : new VariableLengthPropertyDescription[] { 
                        new (
                            BaseType: Create(type.GetElementType()!)
                        )
                    },

            /* string */
            Type when type == typeof(string) 
                => new VariableLengthPropertyDescription[] { 
                    new (
                        BaseType: Create(typeof(byte))
                    )
                },

            /* remaining reference types */
            Type when ReadUtils.IsReferenceOrContainsReferences(type) 
                => CreateCompoundFromObject(type),

            /* non blittable */
            Type when type == typeof(bool) 
                => new FixedPointPropertyDescription[] { 
                    new (
                        BitOffset: 0,
                        BitPrecision: 8
                    )
                },

            /* enumeration */
            Type when type.IsEnum 
                => new EnumerationPropertyDescription[] { 
                    EnumerationPropertyDescription.Create(type)
                },

            /* fixed-point types */
            Type when 
                type == typeof(byte) || 
                type == typeof(ushort) || 
                type == typeof(uint) || 
                type == typeof(ulong) ||
                type == typeof(sbyte) || 
                type == typeof(short) || 
                type == typeof(int) || 
                type == typeof(long) 
                => new FixedPointPropertyDescription[] {
                    new(BitOffset: 0,
                        BitPrecision: (ushort)(typeSize * 8)
                    )
                },

            /* 32 bit floating-point */
            /* https://learn.microsoft.com/en-us/cpp/c-language/type-float */
            Type when type == typeof(float) 
                => new FloatingPointPropertyDescription[] {
                    new(BitOffset: 0,
                        BitPrecision: 32,
                        ExponentLocation: 23,
                        ExponentSize: 8,
                        MantissaLocation: 0,
                        MantissaSize: 23,
                        ExponentBias: 127
                    )
                },

            /* 64 bit floating-point */
            /* https://learn.microsoft.com/en-us/cpp/c-language/type-float */
            Type when type == typeof(double) 
                => new FloatingPointPropertyDescription[] {
                    new(BitOffset: 0,
                        BitPrecision: 64,
                        ExponentLocation: 52,
                        ExponentSize: 11,
                        MantissaLocation: 0,
                        MantissaSize: 52,
                        ExponentBias: 1023
                    )
                },

            /* remaining non-generic value types */
            Type when type.IsValueType && !type.IsPrimitive && !type.IsGenericType 
                => CreateCompoundFromStruct(type),

            /* remaining generic value types */
            Type when type.IsValueType && !type.IsPrimitive 
                => CreateCompoundFromObject(type, useFields: true),

            _ => throw new NotSupportedException($"The data type '{type}' is not supported."),
        };
    }

    private static CompoundPropertyDescription[] CreateCompoundFromStruct(Type type)
    {
        var fieldInfos = type.GetFields(BindingFlags.Public | BindingFlags.Instance);
        var properyDescriptions = new CompoundPropertyDescription[fieldInfos.Length];

        for (int i = 0; i < fieldInfos.Length; i++)
        {
            var fieldInfo = fieldInfos[i];
            var underlyingType = fieldInfo.FieldType;

            properyDescriptions[i] = new CompoundPropertyDescription(
                Name: fieldInfo.Name,
                MemberByteOffset: (ulong)Marshal.OffsetOf(type, fieldInfo.Name),
                MemberTypeMessage: Create(underlyingType)
            );
        }

        return properyDescriptions;
    }

    private static CompoundPropertyDescription[] CreateCompoundFromObject(Type type, bool useFields = false)
    {
        CompoundPropertyDescription[] propertyDescriptions;
        var offset = 0UL;

        if (useFields)
        {
            var fieldInfos = type.GetFields(BindingFlags.Public | BindingFlags.Instance);
            propertyDescriptions = new CompoundPropertyDescription[fieldInfos.Length];

            for (int i = 0; i < fieldInfos.Length; i++)
            {
                var fieldInfo = fieldInfos[i];
                var underlyingType = fieldInfo.FieldType;
                var dataTypeMessage = Create(underlyingType);

                propertyDescriptions[i] = new CompoundPropertyDescription(
                    Name: fieldInfo.Name,
                    MemberByteOffset: offset,
                    MemberTypeMessage: dataTypeMessage
                );

                offset += dataTypeMessage.Size;
            }
        }

        else
        {
            var propertyInfos = type
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(propertyInfo => propertyInfo.CanRead)
                .ToArray();

            propertyDescriptions = new CompoundPropertyDescription[propertyInfos.Length];

            for (int i = 0; i < propertyInfos.Length; i++)
            {
                var propertyInfo = propertyInfos[i];
                var underlyingType = propertyInfo.PropertyType;
                var dataTypeMessage = Create(underlyingType);

                propertyDescriptions[i] = new CompoundPropertyDescription(
                    Name: propertyInfo.Name,
                    MemberByteOffset: offset,
                    MemberTypeMessage: dataTypeMessage
                );

                offset += dataTypeMessage.Size;
            }
        }

        return propertyDescriptions;
    }

    private static DatatypeMessageClass GetClass(
        Type type,
        bool isTopLevel = false)
    {
        return type switch
        {
            /* dictionary */
            Type when typeof(IDictionary).IsAssignableFrom(type) && 
                        type.GenericTypeArguments[0] == typeof(string)
                => isTopLevel

                    /* compound */
                    ? DatatypeMessageClass.Compound

                    /* variable-length list of key-value pairs */
                    : DatatypeMessageClass.VariableLength,

            /* array */
            Type when type.IsArray && type.GetArrayRank() == 1 && type.GetElementType() is not null
                => isTopLevel

                    /* array */
                    ? GetClass(type.GetElementType()!)

                    /* variable-length list of elements */
                    : DatatypeMessageClass.VariableLength,

            /* generic IEnumerable */
            Type when typeof(IEnumerable).IsAssignableFrom(type) && type.IsGenericType
                => isTopLevel

                    /* array */
                    ? GetClass(type.GenericTypeArguments[0])

                    /* variable-length list of elements */
                    : DatatypeMessageClass.VariableLength,

            /* string */
            Type when type == typeof(string) 
                => DatatypeMessageClass.String,

            /* remaining reference types */
            Type when ReadUtils.IsReferenceOrContainsReferences(type) 
                => DatatypeMessageClass.Compound,

            /* non blittable */
            Type when type == typeof(bool)
                => DatatypeMessageClass.FixedPoint,

            /* enumeration */
            Type when type.IsEnum 
                => DatatypeMessageClass.Enumerated,

            /* fixed-point types */
            Type when
                type == typeof(byte) || 
                type == typeof(ushort) || 
                type == typeof(uint) || 
                type == typeof(ulong) ||
                type == typeof(sbyte) || 
                type == typeof(short) || 
                type == typeof(int) || 
                type == typeof(long) 
                => DatatypeMessageClass.FixedPoint,

            /* floating-point types */
            Type when 
                type == typeof(float) ||
                type == typeof(double) 
                => DatatypeMessageClass.FloatingPoint,

            /* remaining value types */
            Type when type.IsValueType && !type.IsPrimitive 
                => DatatypeMessageClass.Compound,

            _ => throw new NotSupportedException($"The data type '{type}' is not supported.")
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

                var size = GetTypeInfo(underlyingType);
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

                var size = GetTypeInfo(underlyingType);
                current = current[size..];
            }
        }

        return result;
    }

    private static (int Size, DatatypeBitFieldDescription BitField) GetTypeInfoOfObject(Type type, bool useFields = false)
    {
        if (useFields)
        {
            var fieldInfos = type
                .GetFields(BindingFlags.Public | BindingFlags.Instance)
                .ToArray();

            var count = (ushort)fieldInfos.Length;

            var size = fieldInfos
                .Aggregate(0, (sum, fieldInfo) => sum + GetTypeInfo(fieldInfo.FieldType).Size);

            var bitField = new CompoundBitFieldDescription(
                MemberCount: count
            );

            return (size, bitField);
        }

        else
        {
            var propertyInfos = type
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(propertyInfo => propertyInfo.CanRead)
                .ToArray();

            var count = (ushort)propertyInfos.Length;

            var size = propertyInfos
                .Aggregate(0, (sum, propertyInfo) => sum + GetTypeInfo(propertyInfo.PropertyType).Size);

            var bitField = new CompoundBitFieldDescription(
                MemberCount: count
            );

            return (size, bitField);
        }
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