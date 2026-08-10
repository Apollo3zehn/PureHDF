using System.Runtime.CompilerServices;

namespace PureHDF.VOL.Native;

internal partial record class AttributeInfoMessage(
    NativeReadContext Context,
    CreationOrderFlags Flags,
    ushort MaximumCreationIndex,
    ulong FractalHeapAddress,
    ulong BTree2NameIndexAddress,
    ulong BTree2CreationOrderIndexAddress
) : Message
{
    private byte _version;
    private FractalHeapHeader? _fractalHeap;
    private BTree2Header<BTree2Record08>? _bTree2NameIndex;
    private BTree2Header<BTree2Record09>? _bTree2CreationOrder;

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
    public async ValueTask<BTree2Header<BTree2Record08>> BTree2NameIndex()
    {
        if (_bTree2NameIndex is null)
        {
            Context.Driver.SeekRelativeToBaseAddress((long)BTree2NameIndexAddress);
            _bTree2NameIndex = await BTree2Header<BTree2Record08>.Decode(Context, DecodeRecord08).ConfigureAwait(false);
        }

        return _bTree2NameIndex;
    }

    // NOTE (async propagation): see FractalHeap() above.
    public async ValueTask<BTree2Header<BTree2Record09>> BTree2CreationOrder()
    {
        if (_bTree2CreationOrder is null)
        {
            Context.Driver.SeekRelativeToBaseAddress((long)BTree2NameIndexAddress);
            _bTree2CreationOrder = await BTree2Header<BTree2Record09>.Decode(Context, DecodeRecord09).ConfigureAwait(false);
        }

        return _bTree2CreationOrder;
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
            Context: context,
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
    private async ValueTask<BTree2Record08> DecodeRecord08() => await BTree2Record08.Decode(Context.Driver).ConfigureAwait(false);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private async ValueTask<BTree2Record09> DecodeRecord09() => await BTree2Record09.Decode(Context.Driver).ConfigureAwait(false);
}