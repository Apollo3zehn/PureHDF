using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace PureHDF;

internal static class DataUtils
{
    static DataUtils()
    {
        MethodInfoCastToArray = typeof(DataUtils)
            .GetMethods(BindingFlags.Static | BindingFlags.NonPublic)
            .Where(methodInfo => methodInfo.IsGenericMethod && methodInfo.Name == nameof(CastToArray))
            .Single();
    }

    public static MethodInfo MethodInfoCastToArray { get; }

    public static bool IsReferenceOrContainsReferences(Type type)
    {
#if NETSTANDARD2_0
        var isSimpleValueType = type.IsPrimitive || type.IsEnum;
        return !isSimpleValueType;
#else
        // TODO cache
        var name = nameof(RuntimeHelpers.IsReferenceOrContainsReferences);
        var flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.Instance;
        var method = typeof(RuntimeHelpers).GetMethod(name, flags)!;
        var generic = method.MakeGenericMethod(type);

        return (bool)generic.Invoke(null, null)!;
#endif
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

    // Warning: do not change the signature without also adapting the _methodInfo variable above
    private static T[] CastToArray<T>(byte[] data) where T : unmanaged
    {
        return MemoryMarshal
            .Cast<byte, T>(data)
            .ToArray();
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
}