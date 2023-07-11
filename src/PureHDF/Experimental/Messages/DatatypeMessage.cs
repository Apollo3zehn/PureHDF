using System.Collections;
using System.Runtime.InteropServices;

namespace PureHDF.VOL.Native;

internal partial record class DatatypeMessage : Message
{
    // reference size                = GHEAP address + GHEAP index
    private const int REFERENCE_SIZE = sizeof(ulong) + sizeof(uint);

    // variable length entry size           length
    private const int VLEN_REFERENCE_SIZE = sizeof(uint) + REFERENCE_SIZE;

    public static DatatypeMessage Create(Type type, object? data = default)
    {
        var typeSize = GetTypeSize(type, data);

        return new DatatypeMessage(
            Size: (uint)typeSize,
            BitField: GetBitFieldDescription(type, data),
            Properties: GetPropertyDescriptions(type, typeSize, data)
        )
        {
            Version = 3,
            Class = GetClass(type)
        };
    }

    private static int GetTypeSize(
        Type type,
        object? data = default)
    {
        var isTopLevel = data is not null;

        // determine size (https://stackoverflow.com/a/4472641)
        return type switch
        {
            /* dictionary */
            Type when typeof(IDictionary).IsAssignableFrom(type) && 
                        type.GenericTypeArguments[0] == typeof(string) 
                => isTopLevel

                    /* compound */
                    ? GetTypeSize(type.GenericTypeArguments[1]) * ((IDictionary)data!).Count

                    /* variable-length list of key-value pairs */
                    : VLEN_REFERENCE_SIZE,

            /* array */
            Type when type.IsArray && type.GetArrayRank() == 1 && type.GetElementType() is not null
                => isTopLevel

                    /* array */
                    ? GetTypeSize(type.GetElementType()!)

                    /* variable-length list of elements */
                    : VLEN_REFERENCE_SIZE,

            /* generic IEnumerable */
            Type when typeof(IEnumerable).IsAssignableFrom(type) && type.IsGenericType
                => isTopLevel

                    /* array */
                    ? GetTypeSize(type.GenericTypeArguments[0])

                    /* variable-length list of elements */
                    : VLEN_REFERENCE_SIZE,

            /* string */
            Type when type == typeof(string) => REFERENCE_SIZE,

            /* remaining reference types */
            Type when ReadUtils.IsReferenceOrContainsReferences(type) => type
                .GetProperties()
                .Where(propertyInfo => propertyInfo.CanRead)
                .Aggregate(0, (sum, propertyInfo) => 
                sum + GetTypeSize(propertyInfo.PropertyType)),

            /* non blittable */
            Type when type == typeof(bool) => 1,

            /* enumeration */
            Type when type.IsEnum => GetTypeSize(Enum.GetUnderlyingType(type)),

            /* remaining non-generic value types */
            Type when type.IsValueType && !type.IsGenericType => Marshal.SizeOf(type),

            /* remaining generic value types */
            Type when type.IsValueType => WriteUtils.SizeOfGenericStruct(type),

            _ => throw new NotSupportedException($"The data type '{type}' is not supported."),
        };
    }

    private static DatatypeBitFieldDescription GetBitFieldDescription(
        Type type,
        object? data = default)
    {
        var isTopLevel = data is not null;

        var endianness = BitConverter.IsLittleEndian 
            ? ByteOrder.LittleEndian 
            : ByteOrder.BigEndian;

        return type switch
        {
            /* dictionary */
            Type when typeof(IDictionary).IsAssignableFrom(type)
                => isTopLevel

                    /* compound */
                    ? new CompoundBitFieldDescription(
                        MemberCount: (ushort)((IDictionary)data!).Count
                    )

                    /* variable-length list of key-value pairs */
                    : new VariableLengthBitFieldDescription(
                        Type: InternalVariableLengthType.Sequence,
                        PaddingType: default,
                        Encoding: default
                    ),

            /* array */
            Type when type.IsArray
                => isTopLevel

                    /* array */
                    ? GetBitFieldDescription(type.GetElementType()!)

                    /* variable-length list of elements */
                    : new VariableLengthBitFieldDescription(
                        Type: InternalVariableLengthType.Sequence,
                        PaddingType: default,
                        Encoding: default
                    ),

            /* generic IEnumerable */
            Type when typeof(IEnumerable).IsAssignableFrom(type) && type.IsGenericType
                => isTopLevel

                    /* array */
                    ? GetBitFieldDescription(type.GenericTypeArguments[0])

                    /* variable-length list of elements */
                    : new VariableLengthBitFieldDescription(
                        Type: InternalVariableLengthType.Sequence,
                        PaddingType: default,
                        Encoding: default
                    ),

            /* string */
            Type when type == typeof(string) => new VariableLengthBitFieldDescription(
                Type: InternalVariableLengthType.String,
                PaddingType: PaddingType.NullTerminate,
                Encoding: CharacterSetEncoding.UTF8
            ),

            /* remaining reference types */
            Type when ReadUtils.IsReferenceOrContainsReferences(type) => new CompoundBitFieldDescription(
                MemberCount: (ushort)type
                    .GetProperties()
                    .Where(propertyInfo => propertyInfo.CanRead)
                    .Count()
            ),

            /* non blittable */
            Type when type == typeof(bool) => new FixedPointBitFieldDescription(
                ByteOrder: endianness,
                PaddingTypeLow: default,
                PaddingTypeHigh: default,
                IsSigned: false
            ),

            /* enumeration */
            Type when type.IsEnum => new EnumerationBitFieldDescription(
                MemberCount: (ushort)Enum
                    .GetNames(type).Length),

            /* unsigned fixed-point types */
            Type when 
                type == typeof(byte) || 
                type == typeof(ushort) || 
                type == typeof(uint) || 
                type == typeof(ulong) => new FixedPointBitFieldDescription(
                ByteOrder: endianness,
                PaddingTypeLow: default,
                PaddingTypeHigh: default,
                IsSigned: false
            ),

            /* signed fixed-point types */
            Type when 
                type == typeof(sbyte) || 
                type == typeof(short) || 
                type == typeof(int) || 
                type == typeof(long) => new FixedPointBitFieldDescription(
                ByteOrder: endianness,
                PaddingTypeLow: default,
                PaddingTypeHigh: default,
                IsSigned: true
            ),

            /* 32 bit floating-point */
            Type when type == typeof(float) => new FloatingPointBitFieldDescription(
                ByteOrder: endianness,
                PaddingTypeLow: default,
                PaddingTypeHigh: default,
                PaddingTypeInternal: default,
                MantissaNormalization: MantissaNormalization.MsbIsNotStoredButImplied,
                SignLocation: 31
            ),

            /* 64 bit floating-point */
            Type when type == typeof(double) => new FloatingPointBitFieldDescription(
                ByteOrder: endianness,
                PaddingTypeLow: default,
                PaddingTypeHigh: default,
                PaddingTypeInternal: default,
                MantissaNormalization: MantissaNormalization.MsbIsNotStoredButImplied,
                SignLocation: 63
            ),

            /* remaining value types */            
            Type when type.IsValueType && !type.IsPrimitive => new CompoundBitFieldDescription(
                MemberCount: (ushort)type
                    .GetFields().Length
            ),

            _ => throw new NotSupportedException($"The data type '{type}' is not supported."),
        };
    }

    private static DatatypePropertyDescription[] GetPropertyDescriptions(
        Type type, 
        int typeSize,
        object? data = default)
    {
        var isTopLevel = data is not null;

        return type switch
        {
            /* dictionary */
            Type when typeof(IDictionary).IsAssignableFrom(type) && 
                        type.GenericTypeArguments[0] == typeof(string) 
                => isTopLevel

                    /* compound */
                    ? CompoundPropertyDescription.Create(type)

                    /* variable-length list of key-value pairs */
                    : new VariableLengthPropertyDescription[] { 
                        new (
                            BaseType: Create(typeof(KeyValuePair<,>).MakeGenericType(type.GenericTypeArguments))
                        )
                    },

            /* array */
            Type when type.IsArray
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
            Type when type == typeof(string) => 
                new VariableLengthPropertyDescription[] { 
                    new (
                        BaseType: Create(typeof(byte))
                    )
                },

            /* remaining reference types */
            Type when ReadUtils.IsReferenceOrContainsReferences(type) =>
                CompoundPropertyDescription.Create(type),

            /* non blittable */
            Type when type == typeof(bool) =>
                new FixedPointPropertyDescription[] { 
                    new (
                        BitOffset: 0,
                        BitPrecision: 8
                    )
                },

            /* enumeration */
            Type when type.IsEnum => 
                new EnumerationPropertyDescription[] { 
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
                type == typeof(long) => new FixedPointPropertyDescription[] {
                new(BitOffset: 0,
                    BitPrecision: (ushort)(typeSize * 8))
            },

            /* 32 bit floating-point */
            /* https://learn.microsoft.com/en-us/cpp/c-language/type-float */
            Type when type == typeof(float) => new FloatingPointPropertyDescription[] {
                new(BitOffset: 0,
                    BitPrecision: 32,
                    ExponentLocation: 23,
                    ExponentSize: 8,
                    MantissaLocation: 0,
                    MantissaSize: 23,
                    ExponentBias: 127)
            },

            /* 64 bit floating-point */
            /* https://learn.microsoft.com/en-us/cpp/c-language/type-float */
            Type when type == typeof(double) => new FloatingPointPropertyDescription[] {
                new(BitOffset: 0,
                    BitPrecision: 64,
                    ExponentLocation: 52,
                    ExponentSize: 11,
                    MantissaLocation: 0,
                    MantissaSize: 52,
                    ExponentBias: 1023)
            },

            /* remaining value types */
            Type when type.IsValueType && !type.IsPrimitive 
                => CompoundPropertyDescription.Create(type),

            _ => throw new NotSupportedException($"The data type '{type}' is not supported."),
        };
    }

    private static DatatypeMessageClass GetClass(Type type)
    {
        return type switch
        {
            Type when 
                type == typeof(byte) || 
                type == typeof(ushort) || 
                type == typeof(uint) || 
                type == typeof(ulong) ||
                type == typeof(sbyte) || 
                type == typeof(short) || 
                type == typeof(int) || 
                type == typeof(long) => DatatypeMessageClass.FixedPoint,

            Type when 
                type == typeof(float) ||
                type == typeof(double) => DatatypeMessageClass.FloatingPoint,

            Type when type.IsEnum => DatatypeMessageClass.Enumerated,

            Type when type.IsValueType && !type.IsPrimitive => DatatypeMessageClass.Compound,

            _ => throw new NotSupportedException($"The data type '{type}' is not supported.")
        };
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