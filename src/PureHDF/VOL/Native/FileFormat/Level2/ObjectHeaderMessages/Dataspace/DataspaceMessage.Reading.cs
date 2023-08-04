namespace PureHDF.VOL.Native;

internal partial record class DataspaceMessage(
    byte Rank,
    DataspaceMessageFlags Flags,
    DataspaceType Type,
    ulong[] Dimensions,
    ulong[] MaxDimensions,
    ulong[]? PermutationIndices
) : Message
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
                throw new NotSupportedException("The dataspace message version must be in the range of 1..2.");

            _version = value;
        }
    }

    public static DataspaceMessage Decode(NativeReadContext context)
    {
        var (driver, superblock) = context;

        var version = driver.ReadByte();
        var rank = driver.ReadByte();
        var flags = (DataspaceMessageFlags)driver.ReadByte();

        DataspaceType type;

        if (version == 1)
        {
            if (rank > 0)
                type = DataspaceType.Simple;

            else
                type = DataspaceType.Scalar;

            driver.ReadBytes(5);
        }
        else
        {
            type = (DataspaceType)driver.ReadByte();
        }

        var dimensionSizes = new ulong[rank];

        var dimensionMaxSizesArePresent = flags.HasFlag(DataspaceMessageFlags.DimensionMaxSizes);
        var permutationIndicesArePresent = flags.HasFlag(DataspaceMessageFlags.PermuationIndices);

        for (int i = 0; i < rank; i++)
        {
            dimensionSizes[i] = superblock.ReadLength(driver);
        }

        ulong[] dimensionMaxSizes;

        if (dimensionMaxSizesArePresent)
        {
            dimensionMaxSizes = new ulong[rank];

            for (int i = 0; i < rank; i++)
            {
                dimensionMaxSizes[i] = superblock.ReadLength(driver);
            }
        }
        
        else
        {
            dimensionMaxSizes = dimensionSizes.ToArray();
        }

        var permutationIndices = default(ulong[]);

        if (permutationIndicesArePresent)
        {
            permutationIndices = new ulong[rank];

            for (int i = 0; i < rank; i++)
            {
                permutationIndices[i] = superblock.ReadLength(driver);
            }
        }

        return new DataspaceMessage(
            rank,
            flags,
            type,
            dimensionSizes,
            dimensionMaxSizes,
            permutationIndices
        )
        {
            Version = version
        };
    }
}