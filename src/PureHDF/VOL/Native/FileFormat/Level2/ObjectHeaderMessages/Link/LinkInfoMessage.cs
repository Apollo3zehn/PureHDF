using System.Runtime.CompilerServices;

namespace PureHDF.VOL.Native;

internal record class LinkInfoMessage(
    NativeContext Context,
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

    public FractalHeapHeader FractalHeap
    {
        get
        {
            if (_fractalHeap is null)
            {
                Context.Driver.Seek((long)FractalHeapAddress, SeekOrigin.Begin);
                _fractalHeap = FractalHeapHeader.Decode(Context);
            }

            return _fractalHeap;
        }
    }

    public BTree2Header<BTree2Record05> BTree2NameIndex
    {
        get
        {
            if (_bTree2NameIndex is null)
            {
                Context.Driver.Seek((long)BTree2NameIndexAddress, SeekOrigin.Begin);
                _bTree2NameIndex = BTree2Header<BTree2Record05>.Decode(Context, DecodeRecord05);
            }

            return _bTree2NameIndex;
        }
    }

    public BTree2Header<BTree2Record06> BTree2CreationOrder
    {
        get
        {
            if (_bTree2CreationOrder is null)
            {
                Context.Driver.Seek((long)BTree2CreationOrderIndexAddress, SeekOrigin.Begin);
                _bTree2CreationOrder = BTree2Header<BTree2Record06>.Decode(Context, DecodeRecord06);
            }

            return _bTree2CreationOrder;
        }
    }

    public static LinkInfoMessage Decode(NativeContext context)
    {
        var (driver, superblock) = context;

        // version
        var version = driver.ReadByte();

        // flags
        var flags = (CreationOrderFlags)driver.ReadByte();

        // maximum creation index
        var maximumCreationIndex = default(ulong);

        if (flags.HasFlag(CreationOrderFlags.TrackCreationOrder))
            maximumCreationIndex = driver.ReadUInt64();

        // fractal heap address
        var fractalHeapAddress = superblock.ReadOffset(driver);

        // BTree2 name index address
        var bTree2NameIndexAddress = superblock.ReadOffset(driver);

        // BTree2 creation order index address
        var bTree2CreationOrderIndexAddress = default(ulong);

        if (flags.HasFlag(CreationOrderFlags.IndexCreationOrder))
            bTree2CreationOrderIndexAddress = superblock.ReadOffset(driver);

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
    private BTree2Record05 DecodeRecord05() => BTree2Record05.Decode(Context.Driver);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private BTree2Record06 DecodeRecord06() => BTree2Record06.Decode(Context.Driver);
}