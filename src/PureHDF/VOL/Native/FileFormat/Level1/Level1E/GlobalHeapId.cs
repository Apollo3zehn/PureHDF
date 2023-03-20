namespace PureHDF.VOL.Native;

internal class GlobalHeapId
{
    #region Fields

    private H5Context _context;

    #endregion

    #region Constructors

    public GlobalHeapId(H5Context context)
    {
        _context = context;
    }

    public GlobalHeapId(H5Context context, H5DriverBase localDriver)
    {
        var (_, superblock) = context;
        _context = context;

        CollectionAddress = superblock.ReadOffset(localDriver);
        ObjectIndex = localDriver.ReadUInt32();
    }

    #endregion

    #region Properties

    public ulong CollectionAddress { get; set; }
    public uint ObjectIndex { get; set; }

    public GlobalHeapCollection Collection
    {
        get
        {
            // TODO: Because Global Heap ID gets a brand new driver (from the attribute), it cannot be reused here. Is this a good approach?
            return H5Cache.GetGlobalHeapObject(_context, CollectionAddress);
        }
    }

    #endregion
}