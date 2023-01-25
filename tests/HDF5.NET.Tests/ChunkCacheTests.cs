using Xunit;

namespace HDF5.NET.Tests.Reading
{
    public class ChunkCacheTests
    {
        [Fact]
        public async Task CanCacheChunk()
        {
            // Arrange
            var cache = new SimpleChunkCache();

            for (int index = 0; index < cache.ChunkSlotCount; index++)
            {
                await cache.GetChunkAsync(new ulong[] { (ulong)index }, () => Task.FromResult(new byte[1].AsMemory()));
            }

            // Act
            for (int index = 0; index < cache.ChunkSlotCount; index++)
            {
                await cache.GetChunkAsync(new ulong[] { (ulong)index }, () => throw new Exception());
            }

            Func<Task> action = () => cache.GetChunkAsync(new ulong[] { 1000 }, () => throw new Exception());

            // Assert
            await Assert.ThrowsAsync<Exception>(action);
        }

        [Fact]
        public async Task CanPreemptChunk_Slots()
        {
            // Arrange
            var cache = new SimpleChunkCache();

            for (int index = 0; index < cache.ChunkSlotCount; index++)
            {
                await cache.GetChunkAsync(new ulong[] { (ulong)index }, () => Task.FromResult(new byte[1].AsMemory()));
            }

            // Act
            var before = cache.ConsumedSlots;
            await cache.GetChunkAsync(new ulong[] { 1000 }, () => Task.FromResult(new byte[1].AsMemory()));
            var after = cache.ConsumedSlots;

            // Assert
            Assert.Equal(521, before);
            Assert.Equal(521, after);
        }

        [Fact]
        public async Task CanPreemptChunk_Size()
        {
            // Arrange
            var cache = new SimpleChunkCache();
            await cache.GetChunkAsync(new ulong[] { 0 }, () => Task.FromResult(new byte[1024 * 1024].AsMemory()));

            // Act
            var before_slots = cache.ConsumedSlots;
            var before_bytes = cache.ConsumedBytes;
            await cache.GetChunkAsync(new ulong[] { 1 }, () => Task.FromResult(new byte[1].AsMemory()));
            var after_slots = cache.ConsumedSlots;
            var after_bytes = cache.ConsumedBytes;

            // Assert
            Assert.Equal(1, before_slots);
            Assert.Equal(1, after_slots);
            Assert.Equal(1024UL * 1024UL, before_bytes);
            Assert.Equal(1UL, after_bytes);
        }

        [Fact]
        public async Task CanPreemptCorrectChunk()
        {
            // Arrange
            var cache = new SimpleChunkCache();

            for (int index = 0; index < cache.ChunkSlotCount; index++)
            {
                if (index == 25)
                    await cache.GetChunkAsync(new ulong[] { (ulong)index }, () => Task.FromResult(new byte[3].AsMemory()));

                else
                    await cache.GetChunkAsync(new ulong[] { (ulong)index }, () => Task.FromResult(new byte[2].AsMemory()));
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100));

            for (int index = 0; index < cache.ChunkSlotCount; index++)
            {
                if (index != 25)
                    await cache.GetChunkAsync(new ulong[] { (ulong)index }, () => throw new Exception());
            }

            var expected = (520UL * 2 + 1 * 3) - 3 + 2;

            // Act
            await cache.GetChunkAsync(new ulong[] { 1000 }, () => Task.FromResult(new byte[2].AsMemory()));
            var actual = cache.ConsumedBytes;

            // Assert
            Assert.Equal(expected, actual);
        }
    }
}