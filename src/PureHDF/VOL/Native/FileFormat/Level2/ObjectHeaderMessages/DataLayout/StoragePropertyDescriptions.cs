using System.Numerics;

namespace PureHDF.VOL.Native;

internal abstract record class StoragePropertyDescription(
//
)
{
    public required ulong Address { get; set; }

    public abstract ushort GetEncodeSize();

    public abstract void Encode(H5DriverBase driver);
};

internal record class CompactStoragePropertyDescription(
    byte[] Data
) : StoragePropertyDescription
{
    public static CompactStoragePropertyDescription Decode(H5DriverBase driver)
    {
        var size = driver.ReadUInt16();

        return new CompactStoragePropertyDescription(
            Data: driver.ReadBytes(size)
        )
        {
            Address = Superblock.UndefinedAddress
        };
    }

    public override ushort GetEncodeSize()
    {
        var encodeSize =
            sizeof(ushort) +
            Data.Length;

        return (ushort)encodeSize;
    }

    public override void Encode(H5DriverBase driver)
    {
        // size
        driver.Write((ushort)Data.Length);

        // data
        driver.Write(Data);
    }
}

internal record class ContiguousStoragePropertyDescription(
    ulong Size
) : StoragePropertyDescription
{
    public static ContiguousStoragePropertyDescription Decode(NativeReadContext context)
    {
        var (driver, superblock) = context;

        // address
        var address = superblock.ReadOffset(driver);

        // 
        var size = superblock.ReadLength(driver);

        return new ContiguousStoragePropertyDescription(
            Size: size
        )
        {
            Address = address
        };
    }

    public override ushort GetEncodeSize()
    {
        var encodeSize =
            sizeof(ulong) +
            sizeof(ulong);

        return (ushort)encodeSize;
    }

    public override void Encode(H5DriverBase driver)
    {
        // address
        driver.Write(Address);

        // size
        driver.Write(Size);
    }
}

internal abstract record class ChunkedStoragePropertyDescription(
    byte Rank
) : StoragePropertyDescription;

internal record class ChunkedStoragePropertyDescription3(
    byte Rank,
    uint[] DimensionSizes
) : ChunkedStoragePropertyDescription(Rank)
{
    public static ChunkedStoragePropertyDescription3 Decode(NativeReadContext context)
    {
        var (driver, superblock) = context;

        // rank
        var rank = driver.ReadByte();

        // address
        var address = superblock.ReadOffset(driver);

        // dimension sizes
        var dimensionSizes = new uint[rank];

        for (uint i = 0; i < rank; i++)
        {
            dimensionSizes[i] = driver.ReadUInt32();
        }

        return new ChunkedStoragePropertyDescription3(
            Rank: rank,
            DimensionSizes: dimensionSizes
        )
        {
            Address = address
        };
    }

    public override ushort GetEncodeSize()
    {
        throw new NotImplementedException();
    }

    public override void Encode(H5DriverBase driver)
    {
        throw new NotImplementedException();
    }
}

internal record class ChunkedStoragePropertyDescription4(
    byte Rank,
    ChunkedStoragePropertyFlags Flags,
    ulong[] DimensionSizes,
    IndexingInformation IndexingInformation
) : ChunkedStoragePropertyDescription(Rank)
{
    public long EncodeAddress { get; private set; }

    public bool IsDirty { get; set; }

    public static ChunkedStoragePropertyDescription4 Decode(NativeReadContext context)
    {
        var (driver, superblock) = context;

        // flags
        var flags = (ChunkedStoragePropertyFlags)driver.ReadByte();

        // rank
        var rank = driver.ReadByte();

        // dimension size encoded length
        var dimensionSizeEncodedLength = driver.ReadByte();

        // dimension sizes
        var dimensionSizes = new ulong[rank];

        for (uint i = 0; i < rank; i++)
        {
            dimensionSizes[i] = ReadUtils.ReadUlong(driver, dimensionSizeEncodedLength);
        }

        // chunk indexing type
        var chunkIndexingType = (ChunkIndexingType)driver.ReadByte();

        // indexing type information
        IndexingInformation indexingTypeInformation = chunkIndexingType switch
        {
            ChunkIndexingType.SingleChunk => SingleChunkIndexingInformation.Decode(context, flags),
            ChunkIndexingType.Implicit => new ImplicitIndexingInformation(),
            ChunkIndexingType.FixedArray => FixedArrayIndexingInformation.Decode(driver),
            ChunkIndexingType.ExtensibleArray => ExtensibleArrayIndexingInformation.Decode(driver),
            ChunkIndexingType.BTree2 => BTree2IndexingInformation.Decode(driver),
            _ => throw new NotSupportedException($"The chunk indexing type '{chunkIndexingType}' is not supported.")
        };

        // address
        var address = superblock.ReadOffset(driver);

        return new ChunkedStoragePropertyDescription4(
            Rank: rank,
            Flags: flags,
            DimensionSizes: dimensionSizes,
            IndexingInformation: indexingTypeInformation
        )
        {
            Address = address
        };
    }

    // HDF5 2.1.x rejects the file at H5Dchunk.c:859 if the stored
    // dimension-size encoded length is not the minimum number of bytes
    // needed to encode the largest dimension. 1.14.x accepted any
    // self-consistent value.
    //
    // This issue is already solved in the C-library and it now behaves
    // according to the spec (https://github.com/HDFGroup/hdf5/issues/6409)
    // but it still make sense to minimize required amount of bytes to
    // encode the dimension size.
    private byte GetDimensionSizeEncodedLength()
    {
        var maxDimensionSize = 1UL;

        for (var i = 0; i < Rank; i++)
        {
            if (DimensionSizes[i] > maxDimensionSize)
                maxDimensionSize = DimensionSizes[i];
        }

        return (byte)((BitOperations.Log2(maxDimensionSize) / 8) + 1);
    }

    public override ushort GetEncodeSize()
    {
        var dimensionSizeEncodedLength = GetDimensionSizeEncodedLength();

        var encodeSize =
            sizeof(byte) +
            sizeof(byte) +
            sizeof(byte) +
            dimensionSizeEncodedLength * Rank +
            sizeof(byte) +
            IndexingInformation.GetEncodeSize(Flags) +
            sizeof(ulong);

        return (ushort)encodeSize;
    }

    public override void Encode(H5DriverBase driver)
    {
        EncodeAddress = driver.Position;

        // flags
        driver.Write((byte)Flags);

        // dimensionality
        driver.Write(Rank);

        // dimension size encoded length
        var dimensionSizeEncodedLength = GetDimensionSizeEncodedLength();
        driver.Write(dimensionSizeEncodedLength);

        // dimension sizes (chunk dims, then the element-size pseudo-dim
        // already set by DataLayoutMessage4.Create)
        for (var i = 0; i < Rank; i++)
        {
            WriteUtils.WriteUlongArbitrary(driver, DimensionSizes[i], dimensionSizeEncodedLength);
        }

        // chunk indexing type
        var indexingType = IndexingInformation switch
        {
            SingleChunkIndexingInformation => ChunkIndexingType.SingleChunk,
            ImplicitIndexingInformation => ChunkIndexingType.Implicit,
            FixedArrayIndexingInformation => ChunkIndexingType.FixedArray,
            ExtensibleArrayIndexingInformation => ChunkIndexingType.ExtensibleArray,
            BTree2IndexingInformation => ChunkIndexingType.BTree2,
            _ => throw new NotSupportedException($"The chunk indexing type '{IndexingInformation.GetType()}' is not supported.")
        };

        driver.Write((byte)indexingType);

        // indexing type information
        IndexingInformation.Encode(driver, Flags);

        // address
        driver.Write(Address);

        IsDirty = false;
    }
}

internal record class VirtualStoragePropertyDescription(
    uint Index
) : StoragePropertyDescription
{
    public static VirtualStoragePropertyDescription Decode(NativeReadContext context)
    {
        var (driver, superblock) = context;

        // address
        var address = superblock.ReadOffset(driver);

        // index
        var index = driver.ReadUInt32();

        return new VirtualStoragePropertyDescription(
            Index: index
        )
        {
            Address = address
        };
    }

    public override ushort GetEncodeSize()
    {
        throw new NotImplementedException();
    }

    public override void Encode(H5DriverBase driver)
    {
        throw new NotImplementedException();
    }
}