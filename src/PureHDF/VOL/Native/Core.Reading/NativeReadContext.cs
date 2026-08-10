namespace PureHDF.VOL.Native;

internal record class NativeReadContext(
    H5DriverBase Driver,
    Superblock Superblock
)
{
    public required H5ReadOptions ReadOptions { get; init; }

    public NativeFile File { get; set; } = default!;

    // NOTE (cache re-key): stable per-file identity used to key the process-wide
    // caches in NativeCache, instead of the H5DriverBase instance. A driver is
    // about to become a per-read-operation allocation, so keying on it would
    // give every read its own (never-cleared) cache. This token is created here,
    // once, by the primary constructor at the single place a "real" file context
    // is built (NativeFile.InternalOpenAsync). A record's "with" expression uses
    // the synthesized copy constructor, which copies the current field value
    // rather than re-running this initializer, so the token's identity survives
    // any `context with { ... }` copy.
    public NativeCacheToken CacheToken { get; init; } = new();
};

/// <summary>
/// Opaque per-file cache key. Only instance identity matters — two tokens are
/// never equal unless they are the same reference.
/// </summary>
internal sealed class NativeCacheToken
{
}