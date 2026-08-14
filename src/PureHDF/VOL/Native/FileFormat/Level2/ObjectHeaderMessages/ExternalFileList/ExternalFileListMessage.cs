namespace PureHDF.VOL.Native;

// CONCURRENCY (retained message): see the note on LinkInfoMessage - this message is retained in
// ObjectHeader.HeaderMessages (and reached through NativeDataset.InternalExternalFileList) for the
// lifetime of the dataset, so it holds no NativeReadContext and caches nothing itself; the heap is
// cached in NativeCache, per file and per address.
//
// Not caching the heap ON THIS MESSAGE is also what keeps every read path off the FILE-LEVEL driver:
// Heap() decodes through the context of the read operation asking for it (H5D_Contiguous ->
// ExternalFileListStream), rather than a context captured at navigation time, which would make the
// first read of an external-file-list dataset unsafe under concurrency.
internal record class ExternalFileListMessage(
    ushort AllocatedSlotCount,
    ushort UsedSlotCount,
    ulong HeapAddress,
    ExternalFileListSlot[] SlotDefinitions
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
            if (value != 1)
                throw new FormatException($"Only version 1 instances of type {nameof(ExternalFileListMessage)} are supported.");

            _version = value;
        }
    }

    public ValueTask<LocalHeap> Heap(NativeReadContext context)
    {
        return NativeCache.GetStructure(context, HeapAddress, LocalHeap.Decode);
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
