namespace PureHDF.VOL.Native;

internal record class DataLayoutMessage12(
    LayoutClass LayoutClass,
    ulong Address,
    byte Rank,
    uint[] DimensionSizes,
    byte[] CompactData
) : DataLayoutMessage(LayoutClass)
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

    public static async ValueTask<DataLayoutMessage12> Decode(NativeReadContext context, byte version)
    {
        /* H5Olayout.c (H5O__layout_decode) */

        var (driver, superblock) = context;

        // rank
        var rank = await driver.ReadByte().ConfigureAwait(false);

        // layout class
        var layoutClass = (LayoutClass)await driver.ReadByte().ConfigureAwait(false);

        // reserved
        await driver.ReadBytes(5).ConfigureAwait(false);

        // data address
        var address = layoutClass switch
        {
            LayoutClass.Compact => ulong.MaxValue, // invalid address
            LayoutClass.Contiguous => await superblock.ReadOffset(driver).ConfigureAwait(false),
            LayoutClass.Chunked => await superblock.ReadOffset(driver).ConfigureAwait(false),
            _ => throw new NotSupportedException($"The layout class '{layoutClass}' is not supported.")
        };

        // dimension sizes (incl. dataset element size if chunked storage)
        var dimensionSizes = new uint[rank];

        for (int i = 0; i < rank; i++)
        {
            dimensionSizes[i] = await driver.ReadUInt32().ConfigureAwait(false);
        }

        // compact data size
        byte[] compactData;

        if (layoutClass == LayoutClass.Compact)
        {
            var compactDataSize = await driver.ReadUInt32().ConfigureAwait(false);
            compactData = await driver.ReadBytes((int)compactDataSize).ConfigureAwait(false);
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