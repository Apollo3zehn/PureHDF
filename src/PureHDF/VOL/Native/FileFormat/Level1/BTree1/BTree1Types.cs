namespace PureHDF.VOL.Native;

internal enum BTree1NodeType : byte
{
    Group = 0,
    RawDataChunks = 1
}

internal interface IBTree1Key
{
    //
}

internal readonly record struct BTree1GroupKey(
    ulong LocalHeapByteOffset
) : IBTree1Key
{
    public static BTree1GroupKey Decode(NativeReadContext context)
    {
        var (driver, superblock) = context;

        return new BTree1GroupKey(
            LocalHeapByteOffset: superblock.ReadLength(driver)
        );
    }
}

internal readonly record struct BTree1RawDataChunksKey(
    uint ChunkSize,
    uint FilterMask,
    ulong[] ScaledChunkOffsets
) : IBTree1Key
{
    public static BTree1RawDataChunksKey Decode(
        H5DriverBase driver, 
        byte rank, 
        ulong[] rawChunkDims)
    {
        // H5Dbtree.c (H5D__btree_decode_key)

        var chunkSize = driver.ReadUInt32();
        var filterMask = driver.ReadUInt32();

        var scaledChunkOffsets = new ulong[rank + 1];

        for (byte i = 0; i < rank + 1; i++) // Do not change this! We MUST read rank + 1 values!
        {
            scaledChunkOffsets[i] = driver.ReadUInt64() / rawChunkDims[i];
        }

        return new BTree1RawDataChunksKey(
            chunkSize,
            filterMask,
            scaledChunkOffsets
        );
    }
}

internal readonly record struct BTree1RawDataChunkUserData(
    ulong ChunkSize,
    ulong ChildAddress,
    uint FilterMask);

internal readonly record struct BTree1SymbolTableUserData(
    SymbolTableEntry SymbolTableEntry
);