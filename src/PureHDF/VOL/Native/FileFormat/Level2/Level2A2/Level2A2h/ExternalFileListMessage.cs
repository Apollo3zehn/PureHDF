namespace PureHDF.VOL.Native;

internal class ExternalFileListMessage : Message
{
    #region Fields

    private H5Context _context;
    private byte _version;

    #endregion

    #region Constructors

    public ExternalFileListMessage(H5Context context)
    {
        var (driver, superblock) = context;
        _context = context;

        // version
        Version = driver.ReadByte();

        // reserved
        driver.ReadBytes(3);

        // TODO: Its value must be at least as large as the value contained in the Used Slots field.
        // allocated slot count
        AllocatedSlotCount = driver.ReadUInt16();

        // used slot count
        UsedSlotCount = driver.ReadUInt16();

        // heap address
        HeapAddress = superblock.ReadOffset(driver);

        // slot definitions
        SlotDefinitions = new ExternalFileListSlot[UsedSlotCount];

        for (int i = 0; i < UsedSlotCount; i++)
        {
            SlotDefinitions[i] = new ExternalFileListSlot(context);
        }
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
            if (value != 1)
                throw new FormatException($"Only version 1 instances of type {nameof(ExternalFileListMessage)} are supported.");

            _version = value;
        }
    }

    public ushort AllocatedSlotCount { get; set; }
    public ushort UsedSlotCount { get; set; }
    public ulong HeapAddress { get; set; }
    public ExternalFileListSlot[] SlotDefinitions { get; set; }

    public LocalHeap Heap
    {
        get
        {
            _context.Driver.Seek((long)HeapAddress, SeekOrigin.Begin);
            return new LocalHeap(_context);
        }
    }

    #endregion
}