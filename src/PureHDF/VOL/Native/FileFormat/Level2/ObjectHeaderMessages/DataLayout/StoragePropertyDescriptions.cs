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

    /// <summary>
    /// The number of bytes used to encode each dimension size.
    /// <para>
    /// The HDF5 library recomputes this value when opening the dataset and fails with
    /// "stored chunk dimension encoding length does not match value calculated from
    /// chunk dimensions" if it differs, so it cannot be a fixed width. This mirrors
    /// <c>H5D__chunk_set_sizes()</c>, which computes
    /// <c>(H5VM_log2_gen(dim[u]) + 8) / 8</c> for every dimension - including the last,
    /// which holds the datatype size - and takes the maximum. That is the minimum
    /// number of bytes able to hold the largest dimension size, so any width from 1 to
    /// 8 is possible (a dimension of 65536 needs 3, not 4).
    /// </para>
    /// </summary>
    internal byte DimensionSizeEncodedLength
    {
        get
        {
            var max = 0UL;

            for (int i = 0; i < Rank; i++)
            {
                if (DimensionSizes[i] > max)
                    max = DimensionSizes[i];
            }

            var length = 1;

            while (length < 8 && max > (1UL << (length * 8)) - 1)
            {
                length++;
            }

            return (byte)length;
        }
    }

    public override ushort GetEncodeSize()
    {
        var encodeSize =
            sizeof(byte) +
            sizeof(byte) +
            sizeof(byte) +
            DimensionSizeEncodedLength * Rank +
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
        var dimensionSizeEncodedLength = DimensionSizeEncodedLength;
        driver.Write(dimensionSizeEncodedLength);

        // dimension sizes
        for (int i = 0; i < Rank; i++)
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