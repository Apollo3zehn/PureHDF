using System.Collections;
using System.Reflection;
using System.Runtime.InteropServices;

namespace PureHDF;

// TODO: cache the generic methods for cases where there are large amount of datasets/attributes with same datatype

internal static class WriteUtils
{
    private static readonly MethodInfo _methodInfoUnmanagedArrayToMemory = typeof(WriteUtils)
        .GetMethod(nameof(UnmanagedArrayToMemory), BindingFlags.NonPublic | BindingFlags.Static)!;

    private static readonly MethodInfo _methodInfoEnumerableToMemory = typeof(WriteUtils)
        .GetMethod(nameof(EnumerableToMemory), BindingFlags.NonPublic | BindingFlags.Static)!;

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

    public static (Type ElementType, bool IsScalar) GetElementType(object data)
    {
        var type = data.GetType();

        if (typeof(IDictionary).IsAssignableFrom(type) && type.GenericTypeArguments[0] == typeof(string))
            return (type, true);

        else if (IsArray(type))
            return (type.GetElementType()!, false);

        else if (IsMemory(type))
            return (type.GenericTypeArguments[0], false);

        else if (typeof(IEnumerable).IsAssignableFrom(type) && type.IsGenericType)
            return (type.GenericTypeArguments[0], false);

        else
            return (type, true);
    }

    public static (Memory<TElement>, ulong[]) ToMemory<T, TElement>(object data)
    {
        var type = typeof(T);

        if (typeof(IDictionary).IsAssignableFrom(type) && type.GenericTypeArguments[0] == typeof(string))
            return ElementToMemory((TElement)data);

        else if (IsArray(type))
            return type.GetElementType()!.IsValueType
                ? InvokeUnmanagedArrayToMemory<TElement>(data)
                : ((Array)data).Rank == 1
                    ? Array1DToMemory((TElement[])data)
                    : ArrayNDToMemory<TElement>((Array)data);

        else if (IsMemory(type))
            return ((Memory<TElement>)data, new ulong[] { (ulong)((Memory<TElement>)data).Length });

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

        var memory = data.Cast<T>().ToArray().AsMemory();

        return (
            memory,
            dimensions
        );
    }

    // Convert unmanaged array to memory
    private static (Memory<T>, ulong[]) InvokeUnmanagedArrayToMemory<T>(object data)
    {
        var genericMethod = _methodInfoUnmanagedArrayToMemory.MakeGenericMethod(typeof(T));
        return ((Memory<T>, ulong[]))genericMethod.Invoke(null, new object[] { data })!;
    }

    private static (Memory<T>, ulong[]) UnmanagedArrayToMemory<T>(Array data) where T : struct
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
    private static void EncodeUnmanagedElement<T>(object source, IH5WriteStream target) where T : unmanaged
    {
        Span<T> sourceArray = stackalloc T[] { (T)source };

        target.Write(MemoryMarshal.AsBytes(sourceArray));
    }
}