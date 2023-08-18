namespace PureHDF.VOL.Native;

internal partial record class DataspaceMessage
{
    public static DataspaceMessage Create(
        ulong[]? dataDimensions,
        ulong[]? dimensions)
    {
        if (dataDimensions is null)
            dataDimensions = dimensions;

        else if (dimensions is null)
            dimensions = dataDimensions;

        if (dataDimensions is null || dimensions is null)
            throw new Exception("This should never happen.");

        var dimensionsTotalSize = dimensions
            .Aggregate(1UL, (x, y) => x * y);

        var dataDimensionsTotalSize = dataDimensions
            .Aggregate(1UL, (x, y) => x * y);

        if (dataDimensions.Any() && dimensionsTotalSize != dataDimensionsTotalSize)
            throw new Exception("The actual number of elements does not match the total number of elements given in the dimensions parameter.");

        var dataspace = new DataspaceMessage(
            Rank: (byte)dimensions.Length,
            Flags: DataspaceMessageFlags.None,
            Type: dataDimensions.Any() ? DataspaceType.Simple : DataspaceType.Scalar,
            Dimensions: dimensions,
            MaxDimensions: dimensions,
            PermutationIndices: default
        )
        {
            Version = 2
        };

        return dataspace;
    }

    public override ushort GetEncodeSize()
    {
        if (Version != 2)
            throw new Exception("Only version 2 dataspace messages are supported.");

        var size =
            sizeof(byte) +
            sizeof(byte) +
            sizeof(byte) +
            sizeof(byte) +
            sizeof(ulong) * Rank +
            (
                Flags.HasFlag(DataspaceMessageFlags.DimensionMaxSizes)
                    ? sizeof(ulong) * Rank
                    : 0
            );
            
        return (ushort)size;
    }

    public override void Encode(H5DriverBase driver)
    {
        driver.Write(Version);
        driver.Write(Rank);
        driver.Write((byte)Flags);

        if (Version == 1)
            driver.Seek(5, SeekOrigin.Current);

        else
            driver.Write((byte)Type);

        var dimensionMaxSizesArePresent = Flags.HasFlag(DataspaceMessageFlags.DimensionMaxSizes);
        var permutationIndicesArePresent = Flags.HasFlag(DataspaceMessageFlags.PermuationIndices);

        for (int i = 0; i < Rank; i++)
        {
            driver.Write(Dimensions[i]);
        }

        if (dimensionMaxSizesArePresent)
        {
            for (int i = 0; i < Rank; i++)
            {
                driver.Write(MaxDimensions[i]);
            }
        }

        if (permutationIndicesArePresent)
        {
            if (PermutationIndices is null)
                throw new Exception("PermutationIndices are present but null.");

            for (int i = 0; i < Rank; i++)
            {
                driver.Write(PermutationIndices[i]);
            }
        }
    }
}