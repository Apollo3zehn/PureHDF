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
            ref Unsafe.As<byte, T>(ref MemoryMarshal.GetArrayDataReference(_array)),
            _array.Length);

        return span;
    }

    // A Memory<T> over a MemoryManager cannot be unwrapped to its array - MemoryMarshal.TryGetArray only
    // answers for array-backed Memory - so a consumer that needs a stable address has no way in but Pin.
    // The async read path hands this buffer straight to the caller's stream, and a stream that marshals
    // bytes across a boundary (a browser's HTTP stack, overlapped I/O) pins its destination to do so.
    public override unsafe MemoryHandle Pin(int elementIndex = 0)
    {
        if ((uint)elementIndex > (uint)_array.Length)
            throw new ArgumentOutOfRangeException(nameof(elementIndex));

        // The array itself is pinned, not a copy of it, so the span and the pointer address the same bytes.
        // GCHandle rather than `fixed`, because the pin has to outlive this method.
        var handle = GCHandle.Alloc(_array, GCHandleType.Pinned);

        var pointer = Unsafe.Add<T>(
            Unsafe.AsPointer(ref Unsafe.As<byte, T>(ref MemoryMarshal.GetArrayDataReference(_array))),
            elementIndex);

        // The handle goes into the MemoryHandle, which frees it on disposal. No `this` is passed as the
        // pinnable, so Unpin is not part of that path and there is no pin count to keep.
        return new MemoryHandle(pointer, handle);
    }

    /// <remarks>
    ///     Deliberately empty. <see cref="Pin" /> hands its <see cref="GCHandle" /> to the returned
    ///     <see cref="MemoryHandle" />, which releases the pin when it is disposed, so there is nothing
    ///     left for this to undo. Throwing here would fail a caller that correctly disposes its handle.
    /// </remarks>
    public override void Unpin()
    {
        // Nothing to do — see the remarks.
    }

    /// <remarks>
    ///     Nothing is owned: the array outlives this manager and belongs to whoever created it. Throwing
    ///     here would punish a caller for the ordinary `using` that <see cref="MemoryManager{T}" /> invites.
    /// </remarks>
    protected override void Dispose(bool disposing)
    {
        // Nothing to release — see the remarks.
    }
}