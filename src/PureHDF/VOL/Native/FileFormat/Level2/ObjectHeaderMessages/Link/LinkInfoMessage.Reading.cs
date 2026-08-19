using System.Runtime.CompilerServices;

namespace PureHDF.VOL.Native;

// CONCURRENCY (retained message): this message ends up in ObjectHeader.HeaderMessages, which
// NativeObject.Header keeps for the whole lifetime of the object - so it OUTLIVES the navigation
// operation that decoded it. It therefore holds no NativeReadContext and caches nothing: a captured
// context would keep pointing at a per-operation driver that has since been handed back to
// NativeOperationSlot and reused by an unrelated read, which is exactly the cursor-corruption class
// the per-operation driver exists to prevent. Every accessor below takes the context of the
// operation calling it and decodes fresh.
//
// The objects the accessors return (FractalHeapHeader, BTree2Header<T>) are transient - created and
// discarded inside one operation - so they may keep capturing a context, and do.
internal partial record class LinkInfoMessage(
    CreationOrderFlags Flags,
    ulong MaximumCreationIndex,
    ulong FractalHeapAddress,
    ulong BTree2NameIndexAddress,
    ulong BTree2CreationOrderIndexAddress
) : Message
{
    private byte _version;

    public required byte Version
    {
        get
        {
            return _version;
        }
        init
        {
            if (value != 0)
                throw new FormatException($"Only version 0 instances of type {nameof(LinkInfoMessage)} are supported.");

            _version = value;
        }
    }

    public ValueTask<FractalHeapHeader> FractalHeap(NativeReadContext context)
    {
        return NativeCache.GetStructure(context, FractalHeapAddress, FractalHeapHeader.Decode);
    }

    // The b-tree header is an implementation detail: it is useless without the key decoder that
    // matches its record type, and that decoder is private here. So the message exposes the two
    // OPERATIONS a caller actually wants instead of handing out a header the caller cannot drive -
    // which also means the caching below is invisible to every caller.
    private ValueTask<BTree2Header<BTree2Record05>> BTree2NameIndex(NativeReadContext context)
    {
        return NativeCache.GetStructure(
            context,
            BTree2NameIndexAddress,
            (DecodeKeyDelegate<BTree2Record05>)DecodeRecord05,
            static (c, dk) => BTree2Header<BTree2Record05>.Decode(c, dk));
    }

    public async IAsyncEnumerable<BTree2Record05> EnumerateNameIndexRecords(NativeReadContext context)
    {
        var nameIndex = await BTree2NameIndex(context).ConfigureAwait(false);

        await foreach (var record in nameIndex.EnumerateRecords(context, DecodeRecord05))
        {
            yield return record;
        }
    }

    public async ValueTask<(bool Success, BTree2Record05 Result)> TryFindNameIndexRecord(
        NativeReadContext context,
        Func<BTree2Record05, ValueTask<int>> compare)
    {
        var nameIndex = await BTree2NameIndex(context).ConfigureAwait(false);

        return await nameIndex.TryFindRecord(context, DecodeRecord05, compare).ConfigureAwait(false);
    }

    public async ValueTask<BTree2Header<BTree2Record06>> BTree2CreationOrder(NativeReadContext context)
    {
        context.Driver.SeekRelativeToBaseAddress((long)BTree2CreationOrderIndexAddress);

        return await BTree2Header<BTree2Record06>
            .Decode(context, DecodeRecord06)
            .ConfigureAwait(false);
    }

    public static async ValueTask<LinkInfoMessage> Decode(NativeReadContext context)
    {

        var (driver, superblock) = context;

        // version
        var version = await driver.ReadByte().ConfigureAwait(false);

        // flags
        var flags = (CreationOrderFlags)await driver.ReadByte().ConfigureAwait(false);

        // maximum creation index
        var maximumCreationIndex = default(ulong);

        if (flags.HasFlag(CreationOrderFlags.TrackCreationOrder))
            maximumCreationIndex = await driver.ReadUInt64().ConfigureAwait(false);

        // fractal heap address
        var fractalHeapAddress = await superblock.ReadOffset(driver).ConfigureAwait(false);

        // BTree2 name index address
        var bTree2NameIndexAddress = await superblock.ReadOffset(driver).ConfigureAwait(false);

        // BTree2 creation order index address
        var bTree2CreationOrderIndexAddress = default(ulong);

        if (flags.HasFlag(CreationOrderFlags.IndexCreationOrder))
            bTree2CreationOrderIndexAddress = await superblock.ReadOffset(driver).ConfigureAwait(false);

        return new LinkInfoMessage(
            flags,
            maximumCreationIndex,
            fractalHeapAddress,
            bTree2NameIndexAddress,
            bTree2CreationOrderIndexAddress
        )
        {
            Version = version
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static async ValueTask<BTree2Record05> DecodeRecord05(NativeReadContext context) => await BTree2Record05.Decode(context.Driver).ConfigureAwait(false);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static async ValueTask<BTree2Record06> DecodeRecord06(NativeReadContext context) => await BTree2Record06.Decode(context.Driver).ConfigureAwait(false);
}
