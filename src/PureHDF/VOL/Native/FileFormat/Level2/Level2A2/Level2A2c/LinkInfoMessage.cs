using System.Runtime.CompilerServices;

namespace PureHDF.VOL.Native;

internal class LinkInfoMessage : Message
{
    #region Fields

    private readonly NativeContext _context;
    private byte _version;

    #endregion

    #region Constructors

    public LinkInfoMessage(NativeContext context)
    {
        _context = context;

        var (driver, superblock) = context;

        // version
        Version = driver.ReadByte();

        // flags
        Flags = (CreationOrderFlags)driver.ReadByte();

        // maximum creation index
        if (Flags.HasFlag(CreationOrderFlags.TrackCreationOrder))
            MaximumCreationIndex = driver.ReadUInt64();

        // fractal heap address
        FractalHeapAddress = superblock.ReadOffset(driver);

        // BTree2 name index address
        BTree2NameIndexAddress = superblock.ReadOffset(driver);

        // BTree2 creation order index address
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
                throw new FormatException($"Only version 0 instances of type {nameof(LinkInfoMessage)} are supported.");

            _version = value;
        }
    }

    public CreationOrderFlags Flags { get; set; }
    public ulong MaximumCreationIndex { get; set; }
    public ulong FractalHeapAddress { get; set; }
    public ulong BTree2NameIndexAddress { get; set; }
    public ulong BTree2CreationOrderIndexAddress { get; set; }

    public FractalHeapHeader FractalHeap
    {
        get
        {
            _context.Driver.Seek((long)FractalHeapAddress, SeekOrigin.Begin);
            return new FractalHeapHeader(_context);
        }
    }

    public BTree2Header<BTree2Record05> BTree2NameIndex
    {
        get
        {
            _context.Driver.Seek((long)BTree2NameIndexAddress, SeekOrigin.Begin);
            return BTree2Header<BTree2Record05>.Decode(_context, DecodeRecord05);
        }
    }

    public BTree2Header<BTree2Record06> BTree2CreationOrder
    {
        get
        {
            _context.Driver.Seek((long)BTree2NameIndexAddress, SeekOrigin.Begin);
            return BTree2Header<BTree2Record06>.Decode(_context, DecodeRecord06);
        }
    }

    #endregion

    #region Methods

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private BTree2Record05 DecodeRecord05() => BTree2Record05.Decode(_context.Driver);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private BTree2Record06 DecodeRecord06() => BTree2Record06.Decode(_context.Driver);

    #endregion
}