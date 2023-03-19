using System.Collections.Concurrent;

namespace PureHDF.VOL.Hsds;

internal record struct CacheEntryKey(string ParentId, string LinkName);

internal class GroupCache
{
    private readonly ConcurrentDictionary<CacheEntryKey, H5Group> _groupMap = new();

    public H5Group GetOrAdd(CacheEntryKey key, Func<CacheEntryKey, H5Group> valueFactory)
    {
        return _groupMap.GetOrAdd(key, valueFactory);
    }
}