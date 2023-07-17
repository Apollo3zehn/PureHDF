using System.Collections;
using System.Reflection;
using System.Reflection.Emit;

namespace PureHDF;

internal static partial class WriteUtils
{
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


    private static readonly MethodInfo _methodInfoMemory = typeof(WriteUtils)
        .GetMethod(nameof(GetMemoryLength), BindingFlags.NonPublic | BindingFlags.Static)!;

    public static int InvokeGetMemoryLengthGeneric(Type type, object data)
    {
        var genericMethod = _methodInfoMemory.MakeGenericMethod(type);
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
}