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

    public override ushort GetEncodeSize()
    {
        var encLen = ComputeEncodedLength(DimensionSizes);

        var encodeSize =
            sizeof(byte) +              // flags
            sizeof(byte) +              // dimensionality (rank)
            sizeof(byte) +              // dimension size encoded length
            encLen * Rank +             // dimension sizes (variable byte width)
            sizeof(byte) +              // chunk indexing type
            IndexingInformation.GetEncodeSize(Flags) +
            sizeof(ulong);              // address

        return (ushort)encodeSize;
    }

    public override void Encode(H5DriverBase driver)
    {
        EncodeAddress = driver.Position;

        // flags
        driver.Write((byte)Flags);

        // dimensionality
        driver.Write(Rank);

        // dimension size encoded length: minimum number of bytes needed to encode
        // the largest chunk dimension. libhdf5's H5D__chunk_set_sizes() in
        // src/H5Dchunk.c strictly enforces (`!=` check) that this value matches its
        // own calculation; hardcoding a different value (e.g. 8) produces files h5py /
        // HDFView / MATLAB / Imaris reject with "stored chunk dimension encoding
        // length does not match value calculated from chunk dimensions".
        var encLen = ComputeEncodedLength(DimensionSizes);
        driver.Write(encLen);

        // dimension sizes (variable byte width per encLen, last entry is element size)
        for (int i = 0; i < Rank; i++)
        {
            WriteUtils.WriteUlongArbitrary(driver, DimensionSizes[i], encLen);
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

    // Mirrors libhdf5 H5D__chunk_set_sizes() byte-counting logic: counts how many
    // 8-bit-shifted iterations bring the largest dimension value to zero. Returns 1
    // even when all dims are zero (encoded length must be at least 1 per HDF5 spec).
    private static byte ComputeEncodedLength(ulong[] dimensionSizes)
    {
        var maxValue = 0UL;

        for (int i = 0; i < dimensionSizes.Length; i++)
        {
            if (dimensionSizes[i] > maxValue)
                maxValue = dimensionSizes[i];
        }

        if (maxValue == 0)
            return 1;

        byte length = 0;

        while (maxValue != 0)
        {
            length++;
            maxValue >>= 8;
        }

        return length;
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