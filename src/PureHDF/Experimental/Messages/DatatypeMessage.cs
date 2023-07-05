using System.Runtime.CompilerServices;

namespace PureHDF.VOL.Native;

internal partial record class DatatypeMessage : Message
{
    public static DatatypeMessageClass GetClass<T>()
    {
        return default(T) switch
        {
            byte or ushort or uint or ulong or
            sbyte or short or int or long => DatatypeMessageClass.FixedPoint,

            float or double => DatatypeMessageClass.FloatingPoint,

            _ => throw new NotSupportedException($"The data type '{typeof(T)}' is not supported."),
        };
    }

    public static DatatypeBitFieldDescription GetBitFieldDescription<T>()
    {
        var endianness = BitConverter.IsLittleEndian 
            ? ByteOrder.LittleEndian 
            : ByteOrder.BigEndian;

        return default(T) switch
        {
            byte or ushort or uint or ulong => new FixedPointBitFieldDescription(
                ByteOrder: endianness,
                PaddingTypeLow: default,
                PaddingTypeHigh: default,
                IsSigned: false
            ),

            sbyte or short or int or long => new FixedPointBitFieldDescription(
                ByteOrder: endianness,
                PaddingTypeLow: default,
                PaddingTypeHigh: default,
                IsSigned: true
            ),

            float or double => new FloatingPointBitFieldDescription(
                ByteOrder: endianness,
                PaddingTypeLow: default,
                PaddingTypeHigh: default,
                PaddingTypeInternal: default,
                MantissaNormalization: default,
                SignLocation: default
            ),

            _ => throw new NotSupportedException($"The data type '{typeof(T)}' is not supported."),
        };
    }

    public static DatatypePropertyDescription[] GetDatatypePropertyDescriptions<T>()
    {
        return default(T) switch
        {
            byte or ushort or uint or ulong or 
            sbyte or short or int or long => new FixedPointPropertyDescription[] {
                new(BitOffset: 0,
                    BitPrecision: (ushort)(Unsafe.SizeOf<T>() * 8))
            },

            // https://learn.microsoft.com/en-us/cpp/c-language/type-float
            float => new FloatingPointPropertyDescription[] {
                new(BitOffset: 0,
                    BitPrecision: 64,
                    ExponentLocation: 52,
                    ExponentSize: 11,
                    MantissaLocation: 0,
                    MantissaSize: 52,
                    ExponentBias: 1023)
            },

            // https://learn.microsoft.com/en-us/cpp/c-language/type-float
            double => new FloatingPointPropertyDescription[] {
                new(BitOffset: 0,
                    BitPrecision: 32,
                    ExponentLocation: 23,
                    ExponentSize: 8,
                    MantissaLocation: 0,
                    MantissaSize: 23,
                    ExponentBias: 127)
            },

            _ => throw new NotSupportedException($"The data type '{typeof(T)}' is not supported."),
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