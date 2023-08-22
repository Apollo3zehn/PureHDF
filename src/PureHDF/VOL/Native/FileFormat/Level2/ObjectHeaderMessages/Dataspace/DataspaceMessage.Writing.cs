namespace PureHDF.VOL.Native;

internal partial record class DataspaceMessage
{
    public static DataspaceMessage Create(
        ulong[]? fileDims)
    {
        if (fileDims is null)
        {
            return new DataspaceMessage(
                Rank: 0,
                Flags: DataspaceMessageFlags.None,
                Type: DataspaceType.Null,
                Dimensions: Array.Empty<ulong>(),
                MaxDimensions: Array.Empty<ulong>(),
                PermutationIndices: default
            )
            {
                Version = 2
            };
        }

        else
        {
            return new DataspaceMessage(
                Rank: (byte)fileDims.Length,
                Flags: DataspaceMessageFlags.None,
                Type: fileDims.Any() ? DataspaceType.Simple : DataspaceType.Scalar,
                Dimensions: fileDims,
                MaxDimensions: fileDims,
                PermutationIndices: default
            )
            {
                Version = 2
            };
        }
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