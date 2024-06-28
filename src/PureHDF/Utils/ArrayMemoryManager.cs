using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace PureHDF;

internal class ArrayMemoryManager<T> : MemoryManager<T>
{
    private readonly Array _array;

    public ArrayMemoryManager(Array array)
    {
        _array = array;
    }

    public override Span<T> GetSpan()
    {
        var span = MemoryMarshal.CreateSpan(
            reference: ref Unsafe.As<byte, T>(ref MemoryMarshal.GetArrayDataReference(_array)),
            length: _array.Length);

        return span;
    }

    public override MemoryHandle Pin(int elementIndex = 0)
    {
        throw new NotImplementedException();
    }

    public override void Unpin()
    {
        throw new NotImplementedException();
    }

    protected override void Dispose(bool disposing)
    {
        throw new NotImplementedException();
    }
}