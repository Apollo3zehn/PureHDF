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

    public async ValueTask<FractalHeapHeader> FractalHeap(NativeReadContext context)
    {
        context.Driver.SeekRelativeToBaseAddress((long)FractalHeapAddress);

        return await FractalHeapHeader.Decode(context).ConfigureAwait(false);
    }

    public async ValueTask<BTree2Header<BTree2Record08>> BTree2NameIndex(NativeReadContext context)
    {
        context.Driver.SeekRelativeToBaseAddress((long)BTree2NameIndexAddress);

        return await BTree2Header<BTree2Record08>
            .Decode(context, () => DecodeRecord08(context))
            .ConfigureAwait(false);
    }

    // PRE-EXISTING (behavior preserved, not introduced here): this seeks BTree2NameIndexAddress, not
    // BTree2CreationOrderIndexAddress. The method has no callers in the tree, so the wrong seek is
    // unreachable today; it is left as found rather than silently fixed as part of a concurrency
    // change - see report.
    public async ValueTask<BTree2Header<BTree2Record09>> BTree2CreationOrder(NativeReadContext context)
    {
        context.Driver.SeekRelativeToBaseAddress((long)BTree2NameIndexAddress);

        return await BTree2Header<BTree2Record09>
            .Decode(context, () => DecodeRecord09(context))
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
