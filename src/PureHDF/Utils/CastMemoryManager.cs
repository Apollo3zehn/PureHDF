using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace PureHDF;

// Ported from PureHDF.VOL.Hsds so the native read path can reinterpret Memory<T> as Memory<byte>
// without copying. Needed because async-first made DecodeDelegate<T> take Memory<T> instead of
// Span<T>, and MemoryMarshal.AsBytes only works on Span.
internal class CastMemoryManager<TFrom, TTo> : MemoryManager<TTo>
        where TFrom : struct
        where TTo : struct
{
    private readonly Memory<TFrom> _from;

    public CastMemoryManager(Memory<TFrom> from) => _from = from;

    public override Span<TTo> GetSpan() => MemoryMarshal.Cast<TFrom, TTo>(_from.Span);

    protected override void Dispose(bool disposing)
    {
        //
    }

    // Pin/Unpin must work here, unlike in the HSDS copy this was ported from: the native read path
    // hands this Memory to Stream.ReadAsync / RandomAccess.ReadAsync, both of which pin it. The
    // pin is delegated to the underlying TFrom memory and the byte offset is recomputed.
    public override MemoryHandle Pin(int elementIndex = 0)
    {
        var byteOffset = elementIndex * Unsafe.SizeOf<TTo>();
        var fromIndex = byteOffset / Unsafe.SizeOf<TFrom>();

        return _from.Slice(fromIndex).Pin();
    }

    public override void Unpin()
    {
        // The handle returned by Pin owns the underlying pin and releases it on Dispose.
    }
}
