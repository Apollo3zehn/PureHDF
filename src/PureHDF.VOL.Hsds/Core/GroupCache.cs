using System.Collections.Concurrent;

namespace PureHDF.VOL.Hsds;

internal record struct CacheEntryKey(string ParentId, string LinkName);

internal class GroupCache
{
    private readonly ConcurrentDictionary<CacheEntryKey, H5Group> _groupMap = new();

    public async Task<H5Group> GetOrAddAsync(CacheEntryKey key, Func<Task<H5Group>> valueFactory)
    {
        if (!_groupMap.TryGetValue(key, out var group))
        {
            group = await valueFactory().ConfigureAwait(false);
            _groupMap.TryAdd(key, group);
        }

        return group;
    }
}