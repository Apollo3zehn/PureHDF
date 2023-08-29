using System.Buffers;

namespace PureHDF;

internal class UnmanagedArrayMemoryManager<T> : MemoryManager<T> where T : struct
{
    private readonly Array _array;

    public UnmanagedArrayMemoryManager(Array array)
    {
        _array = array;
    }

    public override Span<T> GetSpan()
    {
#if NET6_0_OR_GREATER
        var span = MemoryMarshal.CreateSpan(
            reference: ref MemoryMarshal.GetArrayDataReference(_array), 
            length: _array.Length * Unsafe.SizeOf<T>());

        return MemoryMarshal.Cast<byte, T>(span);
#else
        if (_array.Rank != 1)
            throw new Exception("Multi-dimensions arrays are only supported on .NET 6+.");

        else
            return ((T[])_array).AsSpan();
#endif
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