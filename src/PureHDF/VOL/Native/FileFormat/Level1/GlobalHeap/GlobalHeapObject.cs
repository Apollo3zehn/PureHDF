namespace PureHDF.VOL.Native;

internal readonly record struct GlobalHeapObject(
    ushort ObjectIndex,
    ushort ReferenceCount,
    byte[] ObjectData
)
{
    public static GlobalHeapObject Decode(NativeContext context)
    {
        var (driver, superblock) = context;

        // heap object index
        var heapObjectIndex = driver.ReadUInt16();

        if (heapObjectIndex == 0 /* free space object */)
        {
            return new GlobalHeapObject(
                ObjectIndex: default,
                ReferenceCount: default,
                ObjectData: Array.Empty<byte>()
            );
        }

        // reference count
        var referenceCount = driver.ReadUInt16();

        // reserved
        driver.ReadBytes(4);

        // object size
        var objectSize = superblock.ReadLength(driver);

        // object data
        var objectData = driver.ReadBytes((int)objectSize);

        var paddedSize = (int)(Math.Ceiling(objectSize / 8.0) * 8);
        var remainingSize = paddedSize - (int)objectSize;
        driver.ReadBytes(remainingSize);

        return new GlobalHeapObject(
            ObjectIndex: heapObjectIndex,
            ReferenceCount: referenceCount,
            ObjectData: objectData
        );
    }
}