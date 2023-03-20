namespace PureHDF.VOL.Native;

internal class GlobalHeapObject
{
    #region Fields

    private static readonly byte[] _emptyByteArray = Array.Empty<byte>();

    #endregion

    #region Constructors

    public GlobalHeapObject(H5Context context)
    {
        var (driver, superblock) = context;

        // heap object index
        HeapObjectIndex = driver.ReadUInt16();

        if (HeapObjectIndex == 0)
            return;

        // reference count
        ReferenceCount = driver.ReadUInt16();

        // reserved
        driver.ReadBytes(4);

        // object size
        var objectSize = superblock.ReadLength(driver);

        // object data
        ObjectData = driver.ReadBytes((int)objectSize);

        var paddedSize = (int)(Math.Ceiling(objectSize / 8.0) * 8);
        var remainingSize = paddedSize - (int)objectSize;
        driver.ReadBytes(remainingSize);
    }

    #endregion

    #region Properties

    public ushort HeapObjectIndex { get; }
    public ushort ReferenceCount { get; }
    public byte[] ObjectData { get; } = _emptyByteArray;

    #endregion
}