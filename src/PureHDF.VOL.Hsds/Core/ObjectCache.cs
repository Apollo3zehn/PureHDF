using System.Collections.Concurrent;

namespace PureHDF.VOL.Hsds;

internal readonly record struct CacheEntryKey(string ParentId, string LinkName);

internal class ObjectCache
{
    private readonly ConcurrentDictionary<CacheEntryKey, HsdsNamedReference> _referenceMap = new();

    public async Task<HsdsNamedReference> GetOrAddAsync(CacheEntryKey key, Func<Task<HsdsNamedReference>> valueFactory)
    {
        if (!_referenceMap.TryGetValue(key, out var reference))
        {
            reference = await valueFactory().ConfigureAwait(false);
            _referenceMap.TryAdd(key, reference);
        }

        return reference;
    }
}