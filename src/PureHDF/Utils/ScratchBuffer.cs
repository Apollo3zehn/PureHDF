using System.Buffers;

namespace PureHDF;

/// <summary>
///     A short-lived pooled scratch buffer, used where the async read path needs a
///     <see cref="Memory{T}" /> that a <c>stackalloc</c>'d <see cref="Span{T}" /> can no longer
///     provide (a Span cannot cross an <c>await</c>).
/// </summary>
/// <remarks>
///     Deliberately not <c>MemoryPool&lt;T&gt;.Shared.Rent</c>. That returns an
///     <see cref="IMemoryOwner{T}" />, and the default <c>MemoryPool&lt;T&gt;.Shared</c> is an
///     <c>ArrayMemoryPool&lt;T&gt;</c> which heap-allocates a fresh owner object on every single
///     call - about 32 bytes per rent. On a scalar read that is the entire measured allocation
///     regression. <see cref="ArrayPool{T}" /> hands back the array itself and allocates nothing,
///     and wrapping it in a disposable <c>struct</c> keeps the <c>using var</c> call sites intact
///     without boxing (the compiler binds <c>Dispose</c> directly on the struct).
///     <para>
///         Like the pools it wraps, the rented memory is <b>not</b> zeroed - callers that fill only
///         part of it must clear it first.
///     </para>
/// </remarks>
internal readonly struct ScratchBuffer<T> : IDisposable
{
    private readonly T[] _array;

    public ScratchBuffer(int length)
    {
        _array = ArrayPool<T>.Shared.Rent(length);
        Memory = new Memory<T>(_array, 0, length);
    }

    /// <summary>
    ///     The rented memory, sliced to exactly the requested length.
    /// </summary>
    public Memory<T> Memory { get; }

    public void Dispose()
    {
        ArrayPool<T>.Shared.Return(_array);
    }
}
