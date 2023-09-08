using System.Buffers.Binary;

namespace PureHDF.VOL.Native;

internal readonly record struct ReadingGlobalHeapId(
    ulong CollectionAddress,
    uint ObjectIndex
)
{
    public static ReadingGlobalHeapId Decode(Superblock superblock, Span<byte> buffer)
    {
        return new ReadingGlobalHeapId(
            CollectionAddress: superblock.ReadOffset(buffer),
            ObjectIndex: BinaryPrimitives.ReadUInt32LittleEndian(buffer.Slice(superblock.OffsetsSize))
        );
    }
}