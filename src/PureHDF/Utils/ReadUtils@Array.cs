using System.Runtime.InteropServices;

namespace PureHDF;

internal static partial class ReadUtils
{
     private static Array? ReadRawArray(NativeReadContext context, DatatypeMessage baseType, Span<byte> slicedData)
    {
        _ = context.Superblock;

        var fixedPointBitfield = baseType.BitField as FixedPointBitFieldDescription;
        var variableLengthBitfield = baseType.BitField as VariableLengthBitFieldDescription;

        return (baseType.Class, baseType.Size) switch
        {
            (DatatypeMessageClass.FixedPoint, 1) when !fixedPointBitfield!.IsSigned => slicedData.ToArray(),
            (DatatypeMessageClass.FixedPoint, 1) when fixedPointBitfield!.IsSigned => MemoryMarshal.Cast<byte, sbyte>(slicedData).ToArray(),
            (DatatypeMessageClass.FixedPoint, 2) when !fixedPointBitfield!.IsSigned => MemoryMarshal.Cast<byte, ushort>(slicedData).ToArray(),
            (DatatypeMessageClass.FixedPoint, 2) when fixedPointBitfield!.IsSigned => MemoryMarshal.Cast<byte, short>(slicedData).ToArray(),
            (DatatypeMessageClass.FixedPoint, 4) when !fixedPointBitfield!.IsSigned => MemoryMarshal.Cast<byte, uint>(slicedData).ToArray(),
            (DatatypeMessageClass.FixedPoint, 4) when fixedPointBitfield!.IsSigned => MemoryMarshal.Cast<byte, int>(slicedData).ToArray(),
            (DatatypeMessageClass.FixedPoint, 8) when !fixedPointBitfield!.IsSigned => MemoryMarshal.Cast<byte, ulong>(slicedData).ToArray(),
            (DatatypeMessageClass.FixedPoint, 8) when fixedPointBitfield!.IsSigned => MemoryMarshal.Cast<byte, long>(slicedData).ToArray(),

            (DatatypeMessageClass.FloatingPoint, 4) => MemoryMarshal.Cast<byte, float>(slicedData).ToArray(),
            (DatatypeMessageClass.FloatingPoint, 8) => MemoryMarshal.Cast<byte, double>(slicedData).ToArray(),

            (DatatypeMessageClass.String, _)
                => ReadString(context, baseType, slicedData),

            // TODO: Bitfield padding type is not being applied here as well as in the normal Read<T> method.
            (DatatypeMessageClass.BitField, _)
                => slicedData.ToArray(),

            (DatatypeMessageClass.Opaque, _)
                => slicedData.ToArray(),

            (DatatypeMessageClass.Compound, _)
                => ReadCompound(context, baseType, slicedData),

            // TODO: Reference type (from the bit field) is not being considered here as well as in the normal Read<T> method.
            (DatatypeMessageClass.Reference, _)
                => MemoryMarshal.Cast<byte, NativeObjectReference1>(slicedData).ToArray(),

            (DatatypeMessageClass.Enumerated, _)
                => ReadEnumerated(context, baseType, slicedData),

            (DatatypeMessageClass.VariableLength, _) when variableLengthBitfield!.Type == InternalVariableLengthType.String
                => ReadString(context, baseType, slicedData),

            (DatatypeMessageClass.Array, _)
                => ReadArray(context, baseType, slicedData),

            _ => default
        };
    }

    private static Array? ReadArray(NativeReadContext context, DatatypeMessage type, Span<byte> slicedData)
    {
        if (type.Class != DatatypeMessageClass.Array)
            throw new Exception($"This method can only be used for data type class '{DatatypeMessageClass.Array}'.");

        var properties = (ArrayPropertyDescription)type.Properties[0];
        var baseType = properties.BaseType;

        return ReadRawArray(context, baseType, slicedData);
    }
}