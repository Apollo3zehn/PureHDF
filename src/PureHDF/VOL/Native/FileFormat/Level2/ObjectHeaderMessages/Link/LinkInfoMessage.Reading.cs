using System.Runtime.CompilerServices;

namespace PureHDF.VOL.Native;

internal partial record class LinkInfoMessage(
    NativeReadContext Context,
    CreationOrderFlags Flags,
    ulong MaximumCreationIndex,
    ulong FractalHeapAddress,
    ulong BTree2NameIndexAddress,
    ulong BTree2CreationOrderIndexAddress
) : Message
{
    private byte _version;
    private FractalHeapHeader? _fractalHeap;
    private BTree2Header<BTree2Record05>? _bTree2NameIndex;
    private BTree2Header<BTree2Record06>? _bTree2CreationOrder;

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

    // NOTE (async propagation): a lazily-cached property getter cannot await, so
    // this became a method with the same name. Callers outside this file need
    // updating — see report.
    public async ValueTask<FractalHeapHeader> FractalHeap()
    {
        if (_fractalHeap is null)
        {
            Context.Driver.SeekRelativeToBaseAddress((long)FractalHeapAddress);
            _fractalHeap = await FractalHeapHeader.Decode(Context).ConfigureAwait(false);
        }

        return _fractalHeap;
    }

    // NOTE (async propagation): see FractalHeap() above.
    public async ValueTask<BTree2Header<BTree2Record05>> BTree2NameIndex()
    {
        if (_bTree2NameIndex is null)
        {
            Context.Driver.SeekRelativeToBaseAddress((long)BTree2NameIndexAddress);
            _bTree2NameIndex = await BTree2Header<BTree2Record05>.Decode(Context, DecodeRecord05).ConfigureAwait(false);
        }

        return _bTree2NameIndex;
    }

    // NOTE (async propagation): see FractalHeap() above.
    public async ValueTask<BTree2Header<BTree2Record06>> BTree2CreationOrder()
    {
        if (_bTree2CreationOrder is null)
        {
            Context.Driver.SeekRelativeToBaseAddress((long)BTree2CreationOrderIndexAddress);
            _bTree2CreationOrder = await BTree2Header<BTree2Record06>.Decode(Context, DecodeRecord06).ConfigureAwait(false);
        }

        return _bTree2CreationOrder;
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
            context,
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
    private async ValueTask<BTree2Record05> DecodeRecord05() => await BTree2Record05.Decode(Context.Driver).ConfigureAwait(false);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private async ValueTask<BTree2Record06> DecodeRecord06() => await BTree2Record06.Decode(Context.Driver).ConfigureAwait(false);
}