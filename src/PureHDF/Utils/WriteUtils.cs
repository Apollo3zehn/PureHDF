using System.Collections;
using System.Reflection;
using System.Runtime.InteropServices;

namespace PureHDF;

// TODO: cache the generic methods for cases where there are large amount of datasets/attributes with same datatype

internal static class WriteUtils
{
    public static MethodInfo MethodInfoEncodeUnmanagedElement { get; } = typeof(WriteUtils)
        .GetMethod(nameof(EncodeUnmanagedElement), BindingFlags.NonPublic | BindingFlags.Static)!;

    public static void WriteUlongArbitrary(H5DriverBase driver, ulong value, ulong size)
    {
        if (size > 8)
            throw new Exception($"The size {size} is not supported.");

        Span<ulong> valueArray = stackalloc ulong[] { value };
        var valueBytes = MemoryMarshal.AsBytes(valueArray);

        driver.Write(valueBytes[0..(int)size]);
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

    public static (Type ElementType, bool IsScalar) GetElementType(Type type)
    {
        if (typeof(IDictionary).IsAssignableFrom(type) && type.GenericTypeArguments[0] == typeof(string))
            return (type, true);

        else if (DataUtils.IsArray(type))
            return (type.GetElementType()!, false);

        else if (DataUtils.IsMemory(type))
            return (type.GenericTypeArguments[0], false);

        else if (typeof(IEnumerable).IsAssignableFrom(type) && type.IsGenericType)
            return (type.GenericTypeArguments[0], false);

        else
            return (type, true);
    }

    public static (Memory<TElement>, ulong[]?) ToMemory<T, TElement>(object? data)
    {
        var type = typeof(T);

        if (data is null)
            return default;

        if (typeof(IDictionary).IsAssignableFrom(type) && type.GenericTypeArguments[0] == typeof(string))
            return ElementToMemory((TElement)data);

        else if (DataUtils.IsArray(type))
            return ((Array)data).Rank == 1
                ? Array1DToMemory((TElement[])data)
                : ArrayNDToMemory<TElement>((Array)data);

        else if (DataUtils.IsMemory(type))
            return data.Equals(default(T))
                ? default
                : ((Memory<TElement>)data, new ulong[] { (ulong)((Memory<TElement>)data).Length });

        else if (typeof(IEnumerable).IsAssignableFrom(type) && type.IsGenericType)
            return EnumerableToMemory((IEnumerable<TElement>)data);

        else
            return ElementToMemory((TElement)data);
    }

    // Convert element to memory
    private static (Memory<T>, ulong[]) ElementToMemory<T>(T data)
    {
        var memory = new T[] { data }.AsMemory();

        return (
            memory,
            Array.Empty<ulong>()
        );
    }

    // Convert enumerable to memory
    private static (Memory<T>, ulong[]) EnumerableToMemory<T>(IEnumerable<T> data)
    {
        var memory = data.ToArray().AsMemory();

        return (
            memory,
            new ulong[] { (ulong)memory.Length }
        );
    }

    // Convert 1D array to memory
    private static (Memory<T>, ulong[]) Array1DToMemory<T>(T[] data)
    {
        var memory = data.AsMemory();

        return (
            memory,
            new ulong[] { (ulong)memory.Length }
        );
    }

    // Convert ND array to memory
    private static (Memory<T>, ulong[]) ArrayNDToMemory<T>(Array data)
    {
        var dimensions = new ulong[data.Rank];

        for (int i = 0; i < data.Rank; i++)
        {
            dimensions[i] = (ulong)data.GetLongLength(i);
        }

        var memory = new ArrayMemoryManager<T>(data).Memory;

        return (
            memory,
            dimensions
        );
    }

    // Encode unmanaged element
    private static void EncodeUnmanagedElement<T>(object source, IH5WriteStream target) where T : unmanaged
    {
        Span<T> sourceArray = stackalloc T[] { (T)source };

        target.WriteDataset(MemoryMarshal.AsBytes(sourceArray));
    }
}