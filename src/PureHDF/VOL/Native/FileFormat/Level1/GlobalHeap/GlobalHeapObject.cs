namespace PureHDF.VOL.Native;

internal readonly record struct GlobalHeapObject(
    ushort ObjectIndex,
    ushort ReferenceCount,
    byte[] ObjectData
)
{
    public static async ValueTask<GlobalHeapObject> Decode(NativeReadContext context)
    {
        var (driver, superblock) = context;

        // heap object index
        var heapObjectIndex = await driver.ReadUInt16().ConfigureAwait(false);

        if (heapObjectIndex == 0 /* free space object */)
        {
            return new GlobalHeapObject(
                ObjectIndex: default,
                ReferenceCount: default,
                ObjectData: Array.Empty<byte>()
            );
        }

        // reference count
        var referenceCount = await driver.ReadUInt16().ConfigureAwait(false);

        // reserved
        await driver.ReadBytes(4).ConfigureAwait(false);

        // object size
        var objectSize = await superblock.ReadLength(driver).ConfigureAwait(false);

        // object data
        var objectData = await driver.ReadBytes((int)objectSize).ConfigureAwait(false);

        var paddedSize = (int)(Math.Ceiling(objectSize / 8.0) * 8);
        var remainingSize = paddedSize - (int)objectSize;
        await driver.ReadBytes(remainingSize).ConfigureAwait(false);

        return new GlobalHeapObject(
            ObjectIndex: heapObjectIndex,
            ReferenceCount: referenceCount,
            ObjectData: objectData
        );
    }
}