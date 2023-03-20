using System.Collections.Concurrent;

namespace PureHDF.VOL.Hsds;

internal record struct CacheEntryKey(string ParentId, string LinkName);

internal class ObjectCache
{
    private readonly ConcurrentDictionary<CacheEntryKey, HsdsObject> _groupMap = new();

    public async Task<HsdsObject> GetOrAddAsync(CacheEntryKey key, Func<Task<HsdsObject>> valueFactory)
    {
        if (!_groupMap.TryGetValue(key, out var group))
        {
            group = await valueFactory().ConfigureAwait(false);
            _groupMap.TryAdd(key, group);
        }

        return group;
    }
}