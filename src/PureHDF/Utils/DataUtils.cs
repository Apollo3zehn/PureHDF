using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace PureHDF;

internal static class DataUtils
{
    public static MethodInfo MethodInfoIsReferenceOrContainsReferences { get; } = typeof(RuntimeHelpers)
        .GetMethod(nameof(IsReferenceOrContainsReferences), BindingFlags.Public | BindingFlags.Static)!;

    // Cached because it is asked once per dataset on the write path and per read on the read path, and
    // answering it allocates a closed generic method, its type arguments and the boxed result.
    private static readonly ConcurrentDictionary<Type, bool> _isReferenceOrContainsReferencesCache = new();

    public static bool IsReferenceOrContainsReferences(Type type)
    {
        return _isReferenceOrContainsReferencesCache.GetOrAdd(type, static type =>
        {
            var generic = MethodInfoIsReferenceOrContainsReferences.MakeGenericMethod(type);
            return (bool)generic.Invoke(null, null)!;
        });
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