using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace PureHDF;

internal static partial class ReadUtils
{
    private static readonly MethodInfo _methodInfo;

    static ReadUtils()
    {
        _methodInfo = typeof(ReadUtils)
            .GetMethods(BindingFlags.Static | BindingFlags.NonPublic)
            .Where(methodInfo => methodInfo.IsGenericMethod && methodInfo.Name == nameof(CastToArray))
            .Single();
    }

    public static bool IsReferenceOrContainsReferences(Type type)
    {
#if NETSTANDARD2_0
            return false;
#else
        var name = nameof(RuntimeHelpers.IsReferenceOrContainsReferences);
        var flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.Instance;
        var method = typeof(RuntimeHelpers).GetMethod(name, flags)!;
        var generic = method.MakeGenericMethod(type);

        return (bool)generic.Invoke(null, null)!;
#endif
    }

    // Warning: do not change the signature without also adapting the _methodInfo variable above
    private static T[] CastToArray<T>(byte[] data) where T : unmanaged
    {
        return MemoryMarshal
            .Cast<byte, T>(data)
            .ToArray();
    }
}