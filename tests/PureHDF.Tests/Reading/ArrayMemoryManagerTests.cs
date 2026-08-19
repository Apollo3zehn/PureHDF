using Xunit;

namespace PureHDF.Tests.Reading;

/// <summary>
///     The read target handed to a stream is a Memory over ArrayMemoryManager, so whatever a stream needs of
///     that Memory has to work. Pin is the one thing a consumer cannot route around: a Memory over a
///     MemoryManager does not unwrap to its array, so a stream that marshals bytes across a boundary - a
///     browser's HTTP stack, overlapped I/O - pins the destination instead. Reads that stay on the span, which
///     is every synchronous one, never reach this.
/// </summary>
public class ArrayMemoryManagerTests
{
    [Fact]
    public void CanPinAndReadThroughThePointer()
    {
        byte[] array = [1, 2, 3, 4, 5];
        using var manager = new ArrayMemoryManager<byte>(array);

        using var handle = manager.Memory.Pin();

        unsafe
        {
            Assert.Equal(1, ((byte*)handle.Pointer)[0]);
            Assert.Equal(5, ((byte*)handle.Pointer)[4]);
        }
    }

    [Fact]
    public void PinAddressesTheArrayItself()
    {
        // Pinning a copy would be worse than throwing: a stream would fill bytes nobody reads back.
        byte[] array = [0, 0, 0];
        using var manager = new ArrayMemoryManager<byte>(array);

        using var handle = manager.Memory.Pin();

        unsafe
        {
            ((byte*)handle.Pointer)[1] = 42;
        }

        Assert.Equal(42, array[1]);
    }

    [Fact]
    public void PinHonoursTheElementIndex()
    {
        byte[] array = [1, 2, 3, 4, 5];
        using var manager = new ArrayMemoryManager<byte>(array);

        using var handle = manager.Memory.Slice(2).Pin();

        unsafe
        {
            Assert.Equal(3, ((byte*)handle.Pointer)[0]);
        }
    }

    [Fact]
    public void ReleasingTheHandleTwiceIsHarmless()
    {
        // Disposal frees the GCHandle the pin took; a double dispose must not free it twice.
        var manager = new ArrayMemoryManager<byte>(new byte[] { 1 });
        var handle = manager.Memory.Pin();

        handle.Dispose();
        handle.Dispose();

        ((IDisposable)manager).Dispose();
        ((IDisposable)manager).Dispose();
    }

    [Fact]
    public void PinRejectsAnIndexPastTheEnd()
    {
        using var manager = new ArrayMemoryManager<byte>(new byte[] { 1, 2 });

        Assert.Throws<ArgumentOutOfRangeException>(() => manager.Pin(3));
    }

    [Fact]
    public void APinnedReadFillsTheBuffer()
    {
        // A stream writing into this Memory while it is pinned, exactly as a browser's HTTP stack does.
        byte[] expected = [9, 8, 7, 6];
        using var manager = new ArrayMemoryManager<byte>(new byte[4]);
        using var source = new MemoryStream(expected);

        var destination = manager.Memory;

        using (destination.Pin())
        {
            source.ReadExactly(destination.Span);
        }

        Assert.Equal(expected, manager.Memory.ToArray());
    }
}