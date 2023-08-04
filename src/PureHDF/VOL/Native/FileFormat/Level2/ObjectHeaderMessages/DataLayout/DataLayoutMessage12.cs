namespace PureHDF.VOL.Native;

internal record class DataLayoutMessage12(
    LayoutClass LayoutClass,
    ulong Address,
    byte Rank,
    uint[] DimensionSizes,
    byte[] CompactData
) : DataLayoutMessage(LayoutClass, Address)
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
            if (!(1 <= value && value <= 2))
                throw new FormatException($"Only version 1 and version 2 instances of type {nameof(DataLayoutMessage12)} are supported.");

            _version = value;
        }
    }

    public static DataLayoutMessage12 Decode(NativeReadContext context, byte version)
    {
        /* H5Olayout.c (H5O__layout_decode) */

        var (driver, superblock) = context;

        // rank
        var rank = driver.ReadByte();

        // layout class
        var layoutClass = (LayoutClass)driver.ReadByte();

        // reserved
        driver.ReadBytes(5);

        // data address
        var address = layoutClass switch
        {
            LayoutClass.Compact => ulong.MaxValue, // invalid address
            LayoutClass.Contiguous => superblock.ReadOffset(driver),
            LayoutClass.Chunked => superblock.ReadOffset(driver),
            _ => throw new NotSupportedException($"The layout class '{layoutClass}' is not supported.")
        };

        // dimension sizes (incl. dataset element size if chunked storage)
        var dimensionSizes = new uint[rank];

        for (int i = 0; i < rank; i++)
        {
            dimensionSizes[i] = driver.ReadUInt32();
        }

        // compact data size
        byte[] compactData;

        if (layoutClass == LayoutClass.Compact)
        {
            var compactDataSize = driver.ReadUInt32();
            compactData = driver.ReadBytes((int)compactDataSize);
        }

        else
        {
            compactData = Array.Empty<byte>();
        }

        return new DataLayoutMessage12(
            LayoutClass: layoutClass,
            Address: address,
            Rank: rank,
            DimensionSizes: dimensionSizes,
            CompactData: compactData
        )
        {
            Version = version
        };
    }
}