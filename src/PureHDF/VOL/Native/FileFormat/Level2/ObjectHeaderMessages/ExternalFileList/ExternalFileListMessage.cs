namespace PureHDF.VOL.Native;

internal record class ExternalFileListMessage(
    NativeReadContext Context,
    ushort AllocatedSlotCount,
    ushort UsedSlotCount,
    ulong HeapAddress,
    ExternalFileListSlot[] SlotDefinitions
) : Message
{
    private byte _version;
    private LocalHeap _heap;

    public required byte Version
    {
        get
        {
            return _version;
        }
        init
        {
            if (value != 1)
                throw new FormatException($"Only version 1 instances of type {nameof(ExternalFileListMessage)} are supported.");

            _version = value;
        }
    }

    public LocalHeap Heap
    {
        get
        {
            if (_heap.Equals(default))
            {
                Context.Driver.Seek((long)HeapAddress, SeekOrigin.Begin);
                _heap = LocalHeap.Decode(Context);
            }

            return _heap;
        }
    }

    public static ExternalFileListMessage Decode(NativeReadContext context)
    {
        var (driver, superblock) = context;

        // version
        var version = driver.ReadByte();

        // reserved
        driver.ReadBytes(3);

        // TODO: Its value must be at least as large as the value contained in the Used Slots field.
        // allocated slot count
        var allocatedSlotCount = driver.ReadUInt16();

        // used slot count
        var usedSlotCount = driver.ReadUInt16();

        // heap address
        var heapAddress = superblock.ReadOffset(driver);

        // slot definitions
        var slotDefinitions = new ExternalFileListSlot[usedSlotCount];

        for (int i = 0; i < usedSlotCount; i++)
        {
            slotDefinitions[i] = ExternalFileListSlot.Decode(context);
        }

        return new ExternalFileListMessage(
            Context: context,
            AllocatedSlotCount: allocatedSlotCount,
            UsedSlotCount: usedSlotCount,
            HeapAddress: heapAddress,
            SlotDefinitions: slotDefinitions
        )
        {
            Version = version
        };
    }
}