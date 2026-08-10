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

    // NOTE (async propagation): a lazily-cached property getter cannot await, so
    // this became a method with the same name. Callers outside this file need
    // updating — see report.
    //
    // NOTE (per-operation drivers): `Context` here is the FILE-LEVEL context, captured when this
    // message was decoded during navigation - a read operation cannot substitute its own. So the
    // first read of an external-file-list dataset decodes this local heap through the file-level
    // driver and is not concurrency-safe. Pre-existing and out of scope; it is the only remaining
    // read path with that property, and only until `_heap` is populated.
    public async ValueTask<LocalHeap> Heap()
    {
        if (_heap.Equals(default))
        {
            Context.Driver.SeekRelativeToBaseAddress((long)HeapAddress);
            _heap = await LocalHeap.Decode(Context).ConfigureAwait(false);
        }

        return _heap;
    }

    public static async ValueTask<ExternalFileListMessage> Decode(NativeReadContext context)
    {
        var (driver, superblock) = context;

        // version
        var version = await driver.ReadByte().ConfigureAwait(false);

        // reserved
        await driver.ReadBytes(3).ConfigureAwait(false);

        // TODO: Its value must be at least as large as the value contained in the Used Slots field.
        // allocated slot count
        var allocatedSlotCount = await driver.ReadUInt16().ConfigureAwait(false);

        // used slot count
        var usedSlotCount = await driver.ReadUInt16().ConfigureAwait(false);

        // heap address
        var heapAddress = await superblock.ReadOffset(driver).ConfigureAwait(false);

        // slot definitions
        var slotDefinitions = new ExternalFileListSlot[usedSlotCount];

        for (int i = 0; i < usedSlotCount; i++)
        {
            slotDefinitions[i] = await ExternalFileListSlot.Decode(context).ConfigureAwait(false);
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