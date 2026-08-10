using System.Runtime.CompilerServices;

namespace PureHDF.VOL.Native;

// CONCURRENCY (retained message): see the note on LinkInfoMessage - this message is retained in
// ObjectHeader.HeaderMessages for the lifetime of the NativeObject, so it holds no
// NativeReadContext and caches nothing. Every accessor takes the calling operation's context.
internal partial record class AttributeInfoMessage(
    CreationOrderFlags Flags,
    ushort MaximumCreationIndex,
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
                throw new FormatException($"Only version 0 instances of type {nameof(AttributeInfoMessage)} are supported.");

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
    private ValueTask<BTree2Header<BTree2Record08>> BTree2NameIndex(NativeReadContext context)
    {
        return NativeCache.GetStructure(
            context,
            BTree2NameIndexAddress,
            (DecodeKeyDelegate<BTree2Record08>)DecodeRecord08,
            static (c, dk) => BTree2Header<BTree2Record08>.Decode(c, dk));
    }

    public async IAsyncEnumerable<BTree2Record08> EnumerateNameIndexRecords(NativeReadContext context)
    {
        var nameIndex = await BTree2NameIndex(context).ConfigureAwait(false);

        await foreach (var record in nameIndex.EnumerateRecords(context, DecodeRecord08))
        {
            yield return record;
        }
    }

    public async ValueTask<(bool Success, BTree2Record08 Result)> TryFindNameIndexRecord(
        NativeReadContext context,
        Func<BTree2Record08, ValueTask<int>> compare)
    {
        var nameIndex = await BTree2NameIndex(context).ConfigureAwait(false);

        return await nameIndex.TryFindRecord(context, DecodeRecord08, compare).ConfigureAwait(false);
    }

    // PRE-EXISTING (behavior preserved, not introduced here): this seeks BTree2NameIndexAddress, not
    // BTree2CreationOrderIndexAddress. The method has no callers in the tree, so the wrong seek is
    // unreachable today; it is left as found rather than silently fixed as part of a concurrency
    // change - see report.
    public async ValueTask<BTree2Header<BTree2Record09>> BTree2CreationOrder(NativeReadContext context)
    {
        context.Driver.SeekRelativeToBaseAddress((long)BTree2NameIndexAddress);

        return await BTree2Header<BTree2Record09>
            .Decode(context, DecodeRecord09)
            .ConfigureAwait(false);
    }

    public static async ValueTask<AttributeInfoMessage> Decode(NativeReadContext context)
    {
        var (driver, superblock) = context;

        // version
        var version = await driver.ReadByte().ConfigureAwait(false);

        // flags
        var flags = (CreationOrderFlags)await driver.ReadByte().ConfigureAwait(false);

        // maximum creation index
        var maximumCreationIndex = default(ushort);

        if (flags.HasFlag(CreationOrderFlags.TrackCreationOrder))
            maximumCreationIndex = await driver.ReadUInt16().ConfigureAwait(false);

        // fractal heap address
        var fractalHeapAddress = await superblock.ReadOffset(driver).ConfigureAwait(false);

        // b-tree 2 name index address
        var bTree2NameIndexAddress = await superblock.ReadOffset(driver).ConfigureAwait(false);

        // b-tree 2 creation order index address
        var bTree2CreationOrderIndexAddress = default(ulong);

        if (flags.HasFlag(CreationOrderFlags.IndexCreationOrder))
            bTree2CreationOrderIndexAddress = await superblock.ReadOffset(driver).ConfigureAwait(false);

        return new AttributeInfoMessage(
            Flags: flags,
            MaximumCreationIndex: maximumCreationIndex,
            FractalHeapAddress: fractalHeapAddress,
            BTree2NameIndexAddress: bTree2NameIndexAddress,
            BTree2CreationOrderIndexAddress: bTree2CreationOrderIndexAddress
        )
        {
            Version = version
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static async ValueTask<BTree2Record08> DecodeRecord08(NativeReadContext context) => await BTree2Record08.Decode(context.Driver).ConfigureAwait(false);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static async ValueTask<BTree2Record09> DecodeRecord09(NativeReadContext context) => await BTree2Record09.Decode(context.Driver).ConfigureAwait(false);
}
