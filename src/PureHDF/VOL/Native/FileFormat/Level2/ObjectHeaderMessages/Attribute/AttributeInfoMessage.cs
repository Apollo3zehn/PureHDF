using System.Runtime.CompilerServices;

namespace PureHDF.VOL.Native;

internal partial record class AttributeInfoMessage(
    NativeContext Context,
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

    public BTree2Header<BTree2Record08> BTree2NameIndex
    {
        get
        {
            if (_bTree2NameIndex is null)
            {
                Context.Driver.Seek((long)BTree2NameIndexAddress, SeekOrigin.Begin);
                _bTree2NameIndex = BTree2Header<BTree2Record08>.Decode(Context, DecodeRecord08);
            }

            return _bTree2NameIndex;
        }
    }

    public BTree2Header<BTree2Record09> BTree2CreationOrder
    {
        get
        {
            if (_bTree2CreationOrder is null)
            {
                Context.Driver.Seek((long)BTree2NameIndexAddress, SeekOrigin.Begin);
                _bTree2CreationOrder = BTree2Header<BTree2Record09>.Decode(Context, DecodeRecord09);
            }

            return _bTree2CreationOrder;
        }
    }

    public static AttributeInfoMessage Decode(NativeContext context)
    {
        var (driver, superblock) = context;

        // version
        var version = driver.ReadByte();

        // flags
        var flags = (CreationOrderFlags)driver.ReadByte();

        // maximum creation index
        var maximumCreationIndex = default(ushort);

        if (flags.HasFlag(CreationOrderFlags.TrackCreationOrder))
            maximumCreationIndex = driver.ReadUInt16();

        // fractal heap address
        var fractalHeapAddress = superblock.ReadOffset(driver);

        // b-tree 2 name index address
        var bTree2NameIndexAddress = superblock.ReadOffset(driver);

        // b-tree 2 creation order index address
        var bTree2CreationOrderIndexAddress = default(ulong);

        if (flags.HasFlag(CreationOrderFlags.IndexCreationOrder))
            bTree2CreationOrderIndexAddress = superblock.ReadOffset(driver);

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
    private BTree2Record08 DecodeRecord08() => BTree2Record08.Decode(Context.Driver);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private BTree2Record09 DecodeRecord09() => BTree2Record09.Decode(Context.Driver);
}