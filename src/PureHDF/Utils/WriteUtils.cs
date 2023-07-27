using System.Collections;
using System.Reflection;
using System.Runtime.InteropServices;

namespace PureHDF;

internal static class WriteUtils
{
    private static readonly MethodInfo _methodInfoMemoryLength = typeof(WriteUtils)
        .GetMethod(nameof(GetMemoryLength), BindingFlags.NonPublic | BindingFlags.Static)!;

    private static readonly MethodInfo _methodInfoUnmanagedArray = typeof(WriteUtils)
        .GetMethod(nameof(EncodeUnmanagedArray), BindingFlags.NonPublic | BindingFlags.Static)!;

    private static readonly MethodInfo _methodInfoMemory = typeof(WriteUtils)
        .GetMethod(nameof(EncodeMemory), BindingFlags.NonPublic | BindingFlags.Static)!;

    public static MethodInfo MethodInfoElement { get; } = typeof(WriteUtils)
        .GetMethod(nameof(EncodeUnmanagedElement), BindingFlags.NonPublic | BindingFlags.Static)!;

    public static void WriteUlongArbitrary(BinaryWriter driver, ulong value, ulong size)
    {
        switch (size)
        {
            case 1: driver.Write((byte)value); break;
            case 2: driver.Write((ushort)value); break;
            case 4: driver.Write((uint)value); break;
            case 8: driver.Write(value); break;
            default: throw new Exception($"The size {size} is not supported.");
        }
    }

    public static int GetEnumerableLength(IEnumerable data)
    {
        int count = 0;
        var collection = data as ICollection;

        if (collection is not null)
        {
            count = collection.Count;
        }

        else
        {
            var enumerator = data.GetEnumerator();

            while (enumerator.MoveNext())
                count++;
        }

        return count;
    }

    public static int InvokeGetMemoryLengthGeneric(Type type, object data)
    {
        var genericMethod = _methodInfoMemoryLength.MakeGenericMethod(type);
        return (int)genericMethod.Invoke(null, new object[] { data })!;
    }

    private static int GetMemoryLength<T>(Memory<T> data)
    {
        return data.Length;
    }

    public static bool IsArray(Type type)
    {
        return
            type.IsArray &&
            type.GetElementType() is not null;
    }

    public static bool IsMemory(Type type)
    {
        return 
            type.IsGenericType && 
            typeof(Memory<>).Equals(type.GetGenericTypeDefinition());
    }

    // Unmanaged element
    private static void EncodeUnmanagedElement<T>(Stream driver, object data) where T : unmanaged
    {
        Span<T> source = stackalloc T[] { (T)data };

        driver.Write(MemoryMarshal.AsBytes(source));
    }

    // Unmanaged array
    // TODO: cache the generic method for cases where there are large amount of datasets/attributes with different datatype
    public static void InvokeEncodeUnmanagedArray(Type type, Stream driver, object data)
    {
        var genericMethod = _methodInfoUnmanagedArray.MakeGenericMethod(type);
        genericMethod.Invoke(null, new object[] { driver, data });
    }

    private static void EncodeUnmanagedArray<T>(Stream driver, object data) where T : unmanaged
    {
        driver.Write(MemoryMarshal.AsBytes<T>((T[])data));
    }

    // Unmanaged Memory
    private static readonly MethodInfo _methodInfoUnmanagedMemory = typeof(DatatypeMessage)
        .GetMethod(nameof(EncodeUnmanagedMemory), BindingFlags.NonPublic | BindingFlags.Static)!;

    // TODO: cache the generic method for cases where there are large amount of datasets/attributes with different datatype
    public static void InvokeEncodeUnmanagedMemory(Type type, Stream driver, object data)
    {
        var genericMethod = _methodInfoUnmanagedMemory.MakeGenericMethod(type);
        genericMethod.Invoke(null, new object[] { driver, data });
    }

    private static void EncodeUnmanagedMemory<T>(Stream driver, object data) where T : unmanaged
    {
        driver.Write(MemoryMarshal.AsBytes(((Memory<T>)data).Span));
    }

    // Memory
    // TODO: cache the generic method for cases where there are large amount of datasets/attributes with different datatype
    public static void InvokeEncodeMemory(
        Type type, 
        Stream driver, 
        object data, 
        EncodeDelegate elementEncode)
    {
        var genericMethod = _methodInfoMemory.MakeGenericMethod(type);

        genericMethod.Invoke(null, new object[] 
        {
            driver, 
            data, 
            elementEncode
        });
    }

    private static void EncodeMemory<T>(
        Stream driver, 
        object data, 
        EncodeDelegate elementEncode)
    {
        var span = ((Memory<T>)data).Span;

        for (int i = 0; i < span.Length; i++)
        {
            elementEncode(driver, span[i]!);
        }
    }
}