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

    public static async ValueTask<DataspaceMessage> Decode(NativeReadContext context)
    {
        var (driver, superblock) = context;

        var version = await driver.ReadByte().ConfigureAwait(false);
        var rank = await driver.ReadByte().ConfigureAwait(false);
        var flags = (DataspaceMessageFlags)await driver.ReadByte().ConfigureAwait(false);

        DataspaceType type;

        if (version == 1)
        {
            if (rank > 0)
                type = DataspaceType.Simple;

            else
                type = DataspaceType.Scalar;

            await driver.ReadBytes(5).ConfigureAwait(false);
        }
        else
        {
            type = (DataspaceType)await driver.ReadByte().ConfigureAwait(false);
        }

        var dimensionSizes = new ulong[rank];

        var dimensionMaxSizesArePresent = flags.HasFlag(DataspaceMessageFlags.DimensionMaxSizes);
        var permutationIndicesArePresent = flags.HasFlag(DataspaceMessageFlags.PermuationIndices);

        for (int i = 0; i < rank; i++)
        {
            dimensionSizes[i] = await superblock.ReadLength(driver).ConfigureAwait(false);
        }

        ulong[] dimensionMaxSizes;

        if (dimensionMaxSizesArePresent)
        {
            dimensionMaxSizes = new ulong[rank];

            for (int i = 0; i < rank; i++)
            {
                dimensionMaxSizes[i] = await superblock.ReadLength(driver).ConfigureAwait(false);
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
                permutationIndices[i] = await superblock.ReadLength(driver).ConfigureAwait(false);
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