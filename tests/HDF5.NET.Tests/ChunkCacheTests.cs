using System;
using System.Threading.Tasks;
using Xunit;

namespace HDF5.NET.Tests.Reading
{
    public class ChunkCacheTests
    {
        [Fact]
        public void CanCacheChunk()
        {
            // Arrange
            var cache = new SimpleChunkCache();

            for (int index = 0; index < cache.ChunkSlotCount; index++)
            {
                cache.GetChunk(new ulong[] { (ulong)index }, () => new byte[1]);
            }

            // Act
            for (int index = 0; index < cache.ChunkSlotCount; index++)
            {
                cache.GetChunk(new ulong[] { (ulong)index }, () => throw new Exception());
            }

            Action action = () => cache.GetChunk(new ulong[] { 1000 }, () => throw new Exception());

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
                cache.GetChunk(new ulong[]{ (ulong)index }, () => new byte[1]);
            }

            // Act
            var before = cache.ConsumedSlots;
            cache.GetChunk(new ulong[] { 1000 }, () => new byte[1]);
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
            cache.GetChunk(new ulong[] { 0 }, () => new byte[1024 * 1024]);

            // Act
            var before_slots = cache.ConsumedSlots;
            var before_bytes = cache.ConsumedBytes;
            cache.GetChunk(new ulong[] { 1 }, () => new byte[1]);
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
                    cache.GetChunk(new ulong[] { (ulong)index }, () => new byte[3]);

                else
                    cache.GetChunk(new ulong[] { (ulong)index }, () => new byte[2]);
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100));

            for (int index = 0; index < cache.ChunkSlotCount; index++)
            {
                if (index != 25)
                    cache.GetChunk(new ulong[] { (ulong)index }, () => throw new Exception());
            }

            var expected = (520UL * 2 + 1 * 3) - 3 + 2;

            // Act
            cache.GetChunk(new ulong[] { 1000 }, () => new byte[2]);
            var actual = cache.ConsumedBytes;

            // Assert
            Assert.Equal(expected, actual);
        }
    }
}