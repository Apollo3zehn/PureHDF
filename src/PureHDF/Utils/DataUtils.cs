using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace PureHDF;

internal static class DataUtils
{
    public static MethodInfo MethodInfoIsReferenceOrContainsReferences { get; } = typeof(RuntimeHelpers)
        .GetMethod(nameof(IsReferenceOrContainsReferences), BindingFlags.Public | BindingFlags.Static)!;

    public static bool IsReferenceOrContainsReferences(Type type)
    {
#if NETSTANDARD2_0
        var isSimpleValueType = type.IsPrimitive || type.IsEnum;
        return !isSimpleValueType;
#else
        // TODO cache
        var generic = MethodInfoIsReferenceOrContainsReferences.MakeGenericMethod(type);

        return (bool)generic.Invoke(null, null)!;
#endif
    }

    public static void CheckEndianness(ByteOrder byteOrder)
    {
        if (byteOrder == ByteOrder.VaxEndian)
            throw new Exception("VAX-endian byte order is not supported.");

        var isLittleEndian = BitConverter.IsLittleEndian;

        if ((isLittleEndian && byteOrder != ByteOrder.LittleEndian) ||
           (!isLittleEndian && byteOrder != ByteOrder.BigEndian))
        {
            throw new Exception("Byte order conversion is not (yet) support by PureHDF.");
        }
    }

    public static void EnsureEndianness(Span<byte> source, Span<byte> destination, ByteOrder byteOrder, uint bytesOfType)
    {
        if (byteOrder == ByteOrder.VaxEndian)
            throw new Exception("VAX-endian byte order is not supported.");

        var isLittleEndian = BitConverter.IsLittleEndian;

        if ((isLittleEndian && byteOrder != ByteOrder.LittleEndian) ||
           (!isLittleEndian && byteOrder != ByteOrder.BigEndian))
        {
            EndiannessConverter.Convert((int)bytesOfType, source, destination);
        }
    }

    public static bool IsMemory(Type type)
    {
        return 
            type.IsGenericType && 
            typeof(Memory<>).Equals(type.GetGenericTypeDefinition());
    }

    public static bool IsArray(Type type)
    {
        return
            type.IsArray &&
            type.GetElementType() is not null;
    }

    public static int UnmanagedSizeOf(Type type)
    {
        return type switch
        {
            _ when type.IsEnum => Marshal.SizeOf(Enum.GetUnderlyingType(type)),
            _ when type == typeof(bool) => 1,
            _ => Marshal.SizeOf(type)
        };
    }
}