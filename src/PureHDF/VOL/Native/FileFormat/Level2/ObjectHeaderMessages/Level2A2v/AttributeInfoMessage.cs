using System.Runtime.CompilerServices;

namespace PureHDF.VOL.Native;

internal class AttributeInfoMessage : Message
{
    #region Fields

    private NativeContext _context;
    private byte _version;

    #endregion

    #region Constructors

    public AttributeInfoMessage(NativeContext context)
    {
        var (driver, superblock) = context;
        _context = context;

        // version
        Version = driver.ReadByte();

        // flags
        Flags = (CreationOrderFlags)driver.ReadByte();

        // maximum creation index
        if (Flags.HasFlag(CreationOrderFlags.TrackCreationOrder))
            MaximumCreationIndex = driver.ReadUInt16();

        // fractal heap address
        FractalHeapAddress = superblock.ReadOffset(driver);

        // b-tree 2 name index address
        BTree2NameIndexAddress = superblock.ReadOffset(driver);

        // b-tree 2 creation order index address
        if (Flags.HasFlag(CreationOrderFlags.IndexCreationOrder))
            BTree2CreationOrderIndexAddress = superblock.ReadOffset(driver);
    }

    #endregion

    #region Properties

    public byte Version
    {
        get
        {
            return _version;
        }
        set
        {
            if (value != 0)
                throw new FormatException($"Only version 0 instances of type {nameof(AttributeInfoMessage)} are supported.");

            _version = value;
        }
    }

    public CreationOrderFlags Flags { get; set; }
    public ushort MaximumCreationIndex { get; set; }
    public ulong FractalHeapAddress { get; set; }
    public ulong BTree2NameIndexAddress { get; set; }
    public ulong BTree2CreationOrderIndexAddress { get; set; }

    public FractalHeapHeader FractalHeap
    {
        get
        {
            _context.Driver.Seek((long)FractalHeapAddress, SeekOrigin.Begin);
            return FractalHeapHeader.Decode(_context);
        }
    }

    public BTree2Header<BTree2Record08> BTree2NameIndex
    {
        get
        {
            _context.Driver.Seek((long)BTree2NameIndexAddress, SeekOrigin.Begin);
            return BTree2Header<BTree2Record08>.Decode(_context, DecodeRecord08);
        }
    }

    public BTree2Header<BTree2Record09> BTree2CreationOrder
    {
        get
        {
            _context.Driver.Seek((long)BTree2NameIndexAddress, SeekOrigin.Begin);
            return BTree2Header<BTree2Record09>.Decode(_context, DecodeRecord09);
        }
    }

    #endregion

    #region Methods

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private BTree2Record08 DecodeRecord08() => BTree2Record08.Decode(_context.Driver);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private BTree2Record09 DecodeRecord09() => BTree2Record09.Decode(_context.Driver);

    #endregion
}