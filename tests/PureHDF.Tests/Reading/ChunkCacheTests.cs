using Xunit;

namespace PureHDF.Tests.Reading;

public class ChunkCacheTests
{
    [Fact]
    public void CanCacheChunk()
    {
        // Arrange
        var cache = new SimpleChunkCache();

        for (int index = 0; index < cache.ChunkSlotCount; index++)
        {
            cache.GetChunk([(ulong)index], () => new byte[1].AsMemory());
        }

        // Act
        for (int index = 0; index < cache.ChunkSlotCount; index++)
        {
            cache.GetChunk([(ulong)index], () => throw new Exception());
        }

        void action() => cache.GetChunk([1000], () => throw new Exception());

        // Assert
        Assert.Throws<Exception>(action);
    }

    [Fact]
    public void CanPreemptChunk_Slots()
    {
        // Arrange
        var cache = new SimpleChunkCache();

        for (int index = 0; index < cache.ChunkSlotCount; index++)
        {
            cache.GetChunk([(ulong)index], () => new byte[1].AsMemory());
        }

        // Act
        var before = cache.ConsumedSlots;
        cache.GetChunk([1000], () => new byte[1].AsMemory());
        var after = cache.ConsumedSlots;

        // Assert
        Assert.Equal(521, before);
        Assert.Equal(521, after);
    }

    [Fact]
    public void CanPreemptChunk_Size()
    {
        // Arrange
        var cache = new SimpleChunkCache();
        cache.GetChunk([0], () => new byte[1024 * 1024].AsMemory());

        // Act
        var before_slots = cache.ConsumedSlots;
        var before_bytes = cache.ConsumedBytes;
        cache.GetChunk([1], () => new byte[1].AsMemory());
        var after_slots = cache.ConsumedSlots;
        var after_bytes = cache.ConsumedBytes;

        // Assert
        Assert.Equal(1, before_slots);
        Assert.Equal(1, after_slots);
        Assert.Equal(1024UL * 1024UL, before_bytes);
        Assert.Equal(1UL, after_bytes);
    }

    [Fact]
    public void CanPreemptCorrectChunk()
    {
        // Arrange
        var cache = new SimpleChunkCache();

        for (int index = 0; index < cache.ChunkSlotCount; index++)
        {
            if (index == 25)
                cache.GetChunk([(ulong)index], () => new byte[3].AsMemory());

            else
                cache.GetChunk([(ulong)index], () => new byte[2].AsMemory());
        }

        Thread.Sleep(TimeSpan.FromMilliseconds(100));

        for (int index = 0; index < cache.ChunkSlotCount; index++)
        {
            if (index != 25)
                cache.GetChunk([(ulong)index], () => throw new Exception());
        }

        var expected = (520UL * 2 + 1 * 3) - 3 + 2;

        // Act
        cache.GetChunk([1000], () => new byte[2].AsMemory());
        var actual = cache.ConsumedBytes;

        // Assert
        Assert.Equal(expected, actual);
    }
}