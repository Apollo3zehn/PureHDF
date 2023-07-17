#if NETSTANDARD2_0

using System.Buffers;
using System.Runtime.InteropServices;

namespace PureHDF;

internal static class BinaryWriterExtensions
{
    public static void Write(this BinaryWriter driver, Span<byte> data)
    {
        driver.Write(data.ToArray());
    }
}

#endif