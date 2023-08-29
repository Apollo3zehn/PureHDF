using System.Reflection;
using System.Runtime.InteropServices;

namespace PureHDF;

internal static partial class ReadUtils
{
    public static MethodInfo MethodInfoDecodeUnmanagedElement { get; } = typeof(ReadUtils)
        .GetMethod(nameof(DecodeUnmanagedElement), BindingFlags.NonPublic | BindingFlags.Static)!;

    public static ulong ReadUlong(H5DriverBase driver, ulong size)
    {
        return size switch
        {
            1 => driver.ReadByte(),
            2 => driver.ReadUInt16(),
            4 => driver.ReadUInt32(),
            8 => driver.ReadUInt64(),
            _ => ReadUlongArbitrary(driver, size)
        };
    }

    private static ulong ReadUlongArbitrary(H5DriverBase driver, ulong size)
    {
        var result = 0UL;
        var shift = 0;

        for (ulong i = 0; i < size; i++)
        {
            var value = driver.ReadByte();
            result += (ulong)(value << shift);
            shift += 8;
        }

        return result;
    }

        public static bool CanDecodeFromCompound(Type type)
    {
        if (type.IsValueType)
            return !(type.IsPrimitive || type.IsEnum);

        else
            return !type.IsArray;
    }

    public static bool CanDecodeToUnmanaged(
        Type type,
        int fileTypeSize)
    {
        if (DataUtils.IsReferenceOrContainsReferences(type))
            return false;

        var actualType = type switch
        {
            _ when type.IsEnum => Enum.GetUnderlyingType(type),
            _ when type == typeof(bool) => typeof(byte),
            _ => type
        };

        var typeSize = Marshal.SizeOf(actualType);

        return
            typeSize == fileTypeSize;
    }

    // Decode unmanaged element
    public static T DecodeUnmanagedElement<T>(IH5ReadStream source) where T : unmanaged
    {
        Span<T> sourceArray = stackalloc T[] { (T)source };
        source.ReadDataset(MemoryMarshal.AsBytes(sourceArray));

        return sourceArray[0];
    }
}