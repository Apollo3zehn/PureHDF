using System.Runtime.CompilerServices;

namespace PureHDF.VOL.Native;

internal partial record class DatatypeMessage : Message
{
    public static DatatypeMessage Create(Type type, int typeSize)
    {
        return new DatatypeMessage(
            Size: (uint)typeSize,
            BitField: GetBitFieldDescription(type),
            Properties: GetPropertyDescriptions(type, typeSize)
        )
        {
            Version = 3,
            Class = GetClass(type)
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

            Type t when t.BaseType == typeof(Enum) => DatatypeMessageClass.Enumerated,

            _ => throw new NotSupportedException($"The data type '{type}' is not supported.")
        };
    }

    private static DatatypeBitFieldDescription GetBitFieldDescription(Type type)
    {
        var endianness = BitConverter.IsLittleEndian 
            ? ByteOrder.LittleEndian 
            : ByteOrder.BigEndian;

        return type switch
        {
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

            Type t when t.BaseType == typeof(Enum) => new EnumerationBitFieldDescription(
                MemberCount: (ushort)Enum.GetNames(type).Length),

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

            Type t when t.BaseType == typeof(Enum) => new EnumerationPropertyDescription[] {
                EnumerationPropertyDescription.Create(type, typeSize)
            },

            _ => throw new NotSupportedException($"The data type '{type}' is not supported."),
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
            property.Encode(driver);
        }
    }
}