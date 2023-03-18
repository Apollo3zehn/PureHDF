using System.Collections.Concurrent;

namespace PureHDF.VOL.Hsds;

internal record struct CacheEntryKey(string ParentId, string LinkName);

internal class GroupCache
{
    private readonly ConcurrentDictionary<CacheEntryKey, IH5Group> _groupMap = new();

    public IH5Group GetOrAdd(CacheEntryKey key, Func<CacheEntryKey, IH5Group> valueFactory)
    {
        return _groupMap.GetOrAdd(key, valueFactory);
    }
}