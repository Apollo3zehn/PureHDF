using System.Runtime.InteropServices;

namespace PureHDF.VFD;

internal abstract partial class H5DriverBase
{
    public void Write<T>(T value) where T : unmanaged
    {
        Span<T> values = stackalloc T[] { value };
        Write(MemoryMarshal.AsBytes(values));
    }

    public abstract void Write(Span<byte> data);

    public abstract void SetLength(long endAddress);
}