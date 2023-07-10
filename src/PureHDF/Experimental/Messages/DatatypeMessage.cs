using System.Collections;
using System.Runtime.InteropServices;

namespace PureHDF.VOL.Native;

internal partial record class DatatypeMessage : Message
{
    private const int REFERENCE_SIZE = sizeof(ulong) + sizeof(uint);

    public static DatatypeMessage Create(Type type)
    {
        var (elementType, typeSize) = GetTypeInfo(type, isTopLevel: true);

        return new DatatypeMessage(
            Size: (uint)typeSize,
            BitField: GetBitFieldDescription(type, elementType),
            Properties: GetPropertyDescriptions(elementType, typeSize)
        )
        {
            Version = 3,
            Class = GetClass(type)
        };
    }

    private static int GetTypeSize(
        Type type, 
        bool isTopLevel = false)
    {
        // TODO: value tuple is not working yet (because of generic + Marshal.SizeOf(t))
        // TODO: calculate top level dictionary size
        // TODO: can structs contain array / variable length types, i.e. references?
        // TODO: what about T[,], T[][] and T[,,x], T[][][x]?

        // determine size (https://stackoverflow.com/a/4472641)
        return type switch
        {
            /* dictionary */
            Type t when typeof(IDictionary).IsAssignableFrom(t) && 
                        type.GenericTypeArguments[0] == typeof(string) 
                => isTopLevel

                    /* compound */
                    ? // TODO calculate size

                    /* variable-length list of key-value pairs */
                    : REFERENCE_SIZE,

            /* array */
            Type t when t.IsArray && t.GetElementType() is not null
                => isTopLevel
                    ? GetTypeSize(t.GetElementType()!)
                    : REFERENCE_SIZE,

            /* generic IEnumerable */
            Type t when typeof(IEnumerable).IsAssignableFrom(t) && t.IsGenericType
                => isTopLevel
                    ? GetTypeSize(t.GenericTypeArguments[0])
                    : REFERENCE_SIZE,

            /* remaining reference types */
            Type t when ReadUtils.IsReferenceOrContainsReferences(t) => t
                .GetProperties()
                .Where(propertyInfo => propertyInfo.CanRead)
                .Aggregate(0, (sum, propertyInfo) => 
                sum + GetTypeSize(propertyInfo.PropertyType)),

            /* non blittable */
            Type t when t == typeof(bool) => 1,

            /* enumeration */
            Type t when t.IsEnum => GetTypeSize(Enum.GetUnderlyingType(t)),

            /* remaining value types */
            Type t when t.IsValueType => Marshal.SizeOf(t),

            _ => throw new NotSupportedException($"The data type '{type}' is not supported."),
        };
    }

    private static DatatypeBitFieldDescription GetBitFieldDescription(Type type, Type underlyingType)
    {
        var endianness = BitConverter.IsLittleEndian 
            ? ByteOrder.LittleEndian 
            : ByteOrder.BigEndian;

        return underlyingType switch
        {
            Type t when 
                ReadUtils.IsReferenceOrContainsReferences(t) => throw new Exception(),

            Type when type.IsEnum => new EnumerationBitFieldDescription(
                MemberCount: (ushort)Enum.GetNames(type).Length),

            Type t when 
                t == typeof(byte) || 
                t == typeof(ushort) || 
                t == typeof(uint) || 
                t == typeof(ulong) => new FixedPointBitFieldDescription(
                ByteOrder: endianness,
                PaddingTypeLow: default,
                PaddingTypeHigh: default,
                IsSigned: false
            ),

            Type t when 
                t == typeof(sbyte) || 
                t == typeof(short) || 
                t == typeof(int) || 
                t == typeof(long) => new FixedPointBitFieldDescription(
                ByteOrder: endianness,
                PaddingTypeLow: default,
                PaddingTypeHigh: default,
                IsSigned: true
            ),

            Type t when t == typeof(float) => new FloatingPointBitFieldDescription(
                ByteOrder: endianness,
                PaddingTypeLow: default,
                PaddingTypeHigh: default,
                PaddingTypeInternal: default,
                MantissaNormalization: MantissaNormalization.MsbIsNotStoredButImplied,
                SignLocation: 31
            ),

            Type t when t == typeof(double) => new FloatingPointBitFieldDescription(
                ByteOrder: endianness,
                PaddingTypeLow: default,
                PaddingTypeHigh: default,
                PaddingTypeInternal: default,
                MantissaNormalization: MantissaNormalization.MsbIsNotStoredButImplied,
                SignLocation: 63
            ),

            Type t when t.IsValueType && !t.IsPrimitive => new CompoundBitFieldDescription(
                MemberCount: (ushort)type.GetFields().Length
            ),

            _ => throw new NotSupportedException($"The data type '{type}' is not supported."),
        };
    }

    private static DatatypePropertyDescription[] GetPropertyDescriptions(Type type, int typeSize)
    {
        return type switch
        {
            Type t when 
                t == typeof(byte) || 
                t == typeof(ushort) || 
                t == typeof(uint) || 
                t == typeof(ulong) ||
                t == typeof(sbyte) || 
                t == typeof(short) || 
                t == typeof(int) || 
                t == typeof(long) => new FixedPointPropertyDescription[] {
                new(BitOffset: 0,
                    BitPrecision: (ushort)(typeSize * 8))
            },

            // https://learn.microsoft.com/en-us/cpp/c-language/type-float
            Type t when t == typeof(float) => new FloatingPointPropertyDescription[] {
                new(BitOffset: 0,
                    BitPrecision: 32,
                    ExponentLocation: 23,
                    ExponentSize: 8,
                    MantissaLocation: 0,
                    MantissaSize: 23,
                    ExponentBias: 127)
            },

            // https://learn.microsoft.com/en-us/cpp/c-language/type-float
            Type t when t == typeof(double) => new FloatingPointPropertyDescription[] {
                new(BitOffset: 0,
                    BitPrecision: 64,
                    ExponentLocation: 52,
                    ExponentSize: 11,
                    MantissaLocation: 0,
                    MantissaSize: 52,
                    ExponentBias: 1023)
            },

            Type t when t.IsEnum => new EnumerationPropertyDescription[] {
                EnumerationPropertyDescription.Create(type)
            },

            Type t when t.IsValueType && !t.IsPrimitive => CompoundPropertyDescription.Create(type),

            _ => throw new NotSupportedException($"The data type '{type}' is not supported."),
        };
    }

    private static DatatypeMessageClass GetClass(Type type)
    {
        return type switch
        {
            Type t when 
                t == typeof(byte) || 
                t == typeof(ushort) || 
                t == typeof(uint) || 
                t == typeof(ulong) ||
                t == typeof(sbyte) || 
                t == typeof(short) || 
                t == typeof(int) || 
                t == typeof(long) => DatatypeMessageClass.FixedPoint,

            Type t when 
                t == typeof(float) ||
                t == typeof(double) => DatatypeMessageClass.FloatingPoint,

            Type t when t.IsEnum => DatatypeMessageClass.Enumerated,

            Type t when t.IsValueType && !t.IsPrimitive => DatatypeMessageClass.Compound,

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