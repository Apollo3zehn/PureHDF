namespace PureHDF.VOL.Native;

internal partial record class DataspaceMessage
{
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

    public override void Encode(BinaryWriter driver)
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
            driver.Write(DimensionSizes[i]);
        }

        if (dimensionMaxSizesArePresent)
        {
            for (int i = 0; i < Rank; i++)
            {
                driver.Write(DimensionMaxSizes[i]);
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