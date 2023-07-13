using System.Collections;
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

    public static ulong[] CalculateDataDimensions(object data)
    {
        if (data is IDictionary)
        {
            return new ulong[] { 1 };
        }

        else if (data is IEnumerable enumerable)
        {
            var count = GetEnumerableLength(enumerable);
            return new ulong[] { (ulong)count };
        }

        else
        {
            return Array.Empty<ulong>();
        }
    }
}