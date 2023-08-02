using System.Collections;
using System.Reflection;
using System.Runtime.InteropServices;

namespace PureHDF;

// TODO: cache the generic methods for cases where there are large amount of datasets/attributes with same datatype

internal static class WriteUtils
{
    private static readonly MethodInfo _methodInfoMemoryLength = typeof(WriteUtils)
        .GetMethod(nameof(GetMemoryLength), BindingFlags.NonPublic | BindingFlags.Static)!;

    private static readonly MethodInfo _methodInfoUnmanagedArrayToMemory = typeof(WriteUtils)
        .GetMethod(nameof(UnmanagedArrayToMemory), BindingFlags.NonPublic | BindingFlags.Static)!;

    private static readonly MethodInfo _methodInfoArray1DToMemory = typeof(WriteUtils)
        .GetMethod(nameof(Array1DToMemory), BindingFlags.NonPublic | BindingFlags.Static)!;

    private static readonly MethodInfo _methodInfoArrayNDToMemory = typeof(WriteUtils)
        .GetMethod(nameof(ArrayNDToMemory), BindingFlags.NonPublic | BindingFlags.Static)!;

    private static readonly MethodInfo _methodInfoEnumerableToMemory = typeof(WriteUtils)
        .GetMethod(nameof(EnumerableToMemory), BindingFlags.NonPublic | BindingFlags.Static)!;

    private static readonly MethodInfo _methodInfoEncodeUnmanagedMemory = typeof(WriteUtils)
        .GetMethod(nameof(EncodeUnmanagedMemory), BindingFlags.NonPublic | BindingFlags.Static)!;

    private static readonly MethodInfo _methodInfoEncodeMemory = typeof(WriteUtils)
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

    public static (object, ulong[]) EnsureMemoryOrScalar(object data)
    {
        var type = data.GetType();

        if (typeof(IDictionary).IsAssignableFrom(type) && type.GenericTypeArguments[0] == typeof(string))
            return (data, Array.Empty<ulong>());

        else if (IsArray(type))
            return type.GetElementType()!.IsValueType
                ? InvokeUnmanagedArrayToMemory(type.GetElementType()!, data)
                : ((Array)data).Rank == 1
                    ? InvokeArray1DToMemory(type.GetElementType()!, data)
                    : InvokeArrayNDToMemory(type.GetElementType()!, data);

        else if (IsMemory(type))
            return (data, new ulong[] { (ulong)InvokeGetMemoryLength(type.GenericTypeArguments[0], data) });

        else if (typeof(IEnumerable).IsAssignableFrom(type) && type.IsGenericType)
            return InvokeEnumerableToMemory(type.GenericTypeArguments[0], data);

        else
            return (data, Array.Empty<ulong>());
    }

    // Get memory length
    private static int InvokeGetMemoryLength(Type type, object data)
    {
        var genericMethod = _methodInfoMemoryLength.MakeGenericMethod(type);
        return (int)genericMethod.Invoke(null, new object[] { data })!;
    }

    private static int GetMemoryLength<T>(Memory<T> data)
    {
        return data.Length;
    }

    // Convert enumerable to memory
    private static (object, ulong[]) InvokeEnumerableToMemory(Type type, object data)
    {
        var genericMethod = _methodInfoEnumerableToMemory.MakeGenericMethod(type);
        return ((object, ulong[]))genericMethod.Invoke(null, new object[] { data })!;
    }

    private static (object, ulong[]) EnumerableToMemory<T>(IEnumerable<T> data)
    {
        var memory = data.ToArray().AsMemory();
        
        return (
            memory,
            new ulong[] { (ulong)memory.Length }
        );
    }

    // Convert 1D array to memory
    private static (object, ulong[]) InvokeArray1DToMemory(Type type, object data)
    {
        var genericMethod = _methodInfoArray1DToMemory.MakeGenericMethod(type);
        return ((object, ulong[]))genericMethod.Invoke(null, new object[] { data })!;
    }

    private static (object, ulong[]) Array1DToMemory<T>(T[] data)
    {
        var memory = data.AsMemory();

        return (
            memory,
            new ulong[] { (ulong)memory.Length }
        );
    }

    // Convert ND array to memory
    private static (object, ulong[]) InvokeArrayNDToMemory(Type type, object data)
    {
        var genericMethod = _methodInfoArrayNDToMemory.MakeGenericMethod(type);
        return ((object, ulong[]))genericMethod.Invoke(null, new object[] { data })!;
    }

    private static (object, ulong[]) ArrayNDToMemory<T>(Array data)
    {
        var dimensions = new ulong[data.Rank];

        for (int i = 0; i < data.Rank; i++)
        {
            dimensions[i] = (ulong)data.GetLongLength(i);
        }

        var memory = data.Cast<T>().ToArray().AsMemory();

        return (
            memory,
            dimensions
        );
    }

    // Convert unmanaged array to memory
    private static (object, ulong[]) InvokeUnmanagedArrayToMemory(Type type, object data)
    {
        var genericMethod = _methodInfoUnmanagedArrayToMemory.MakeGenericMethod(type);
        return ((object, ulong[]))genericMethod.Invoke(null, new object[] { data })!;
    }

    private static (object, ulong[]) UnmanagedArrayToMemory<T>(Array data) where T : struct
    {
        var dimensions = new ulong[data.Rank];

        for (int i = 0; i < data.Rank; i++)
        {
            dimensions[i] = (ulong)data.GetLongLength(i);
        }

        var memory = new UnmanagedArrayMemoryManager<T>(data).Memory;

        return (
            memory,
            dimensions
        );
    }

    // Encode unmanaged element
    private static void EncodeUnmanagedElement<T>(Stream driver, object data) where T : unmanaged
    {
        Span<T> source = stackalloc T[] { (T)data };

        driver.Write(MemoryMarshal.AsBytes(source));
    }

    // Encode unmanaged Memory
    public static void InvokeEncodeUnmanagedMemory(Type type, Stream driver, object data)
    {
        var genericMethod = _methodInfoEncodeUnmanagedMemory.MakeGenericMethod(type);
        genericMethod.Invoke(null, new object[] { driver, data });
    }

    private static void EncodeUnmanagedMemory<T>(Stream driver, object data) where T : unmanaged
    {
        driver.Write(MemoryMarshal.AsBytes(((Memory<T>)data).Span));
    }

    // Encode memory
    public static void InvokeEncodeMemory(
        Type type, 
        Stream driver, 
        object data, 
        EncodeDelegate elementEncode)
    {
        var genericMethod = _methodInfoEncodeMemory.MakeGenericMethod(type);

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