using System.Collections.Concurrent;
using Microsoft.JSInterop;
using PureHDF;

namespace PureHdfWasm;

public sealed class BlobSliceStream : IConcurrentStream, IAsyncDisposable
{
    private const int SlotSize = 64 * 1024;

    private readonly IJSObjectReference _module;
    private readonly IJSObjectReference _blob;
    private readonly ConcurrentDictionary<long, byte[]> _cache = new();
    private bool _disposed;

    public BlobSliceStream(IJSObjectReference module, IJSObjectReference blob, long length)
    {
        _module = module;
        _blob = blob;
        Length = length;
    }

    public long Length { get; }

    public async ValueTask ReadDatasetAsync(long offset, Memory<byte> buffer)
    {
        var bytes = await _module
            .InvokeAsync<byte[]>("readBlobSlice", _blob, offset, offset + buffer.Length)
            .ConfigureAwait(false);

        bytes.AsSpan().CopyTo(buffer.Span);
    }

    public async ValueTask ReadMetadataAsync(long offset, Memory<byte> buffer)
    {
        var position = offset;
        var remaining = buffer;

        while (remaining.Length > 0)
        {
            var slotIndex = position / SlotSize;
            var slotOffset = (int)(position - slotIndex * SlotSize);
            var slotEnd = (int)Math.Min(SlotSize, Length - slotIndex * SlotSize);
            var available = slotEnd - slotOffset;
            var toCopy = Math.Min(available, remaining.Length);

            var slot = await _cache.GetOrAddAsync(slotIndex, async idx =>
            {
                var slotStart = idx * SlotSize;
                var slotStop = Math.Min(slotStart + SlotSize, Length);
                return await _module
                    .InvokeAsync<byte[]>("readBlobSlice", _blob, slotStart, slotStop)
                    .ConfigureAwait(false);
            }).ConfigureAwait(false);

            new ReadOnlySpan<byte>(slot, slotOffset, toCopy).CopyTo(remaining.Span);

            position += toCopy;
            remaining = remaining[toCopy..];
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        _ = _blob.DisposeAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;

        await _blob.DisposeAsync().ConfigureAwait(false);
    }
}

internal static class ConcurrentDictionaryExtensions
{
    public static async ValueTask<TValue> GetOrAddAsync<TKey, TValue>(
        this ConcurrentDictionary<TKey, TValue> dict,
        TKey key,
        Func<TKey, Task<TValue>> factory)
        where TKey : notnull
    {
        if (dict.TryGetValue(key, out var existing))
            return existing;

        existing = await factory(key).ConfigureAwait(false);
        return dict.GetOrAdd(key, existing);
    }
}
