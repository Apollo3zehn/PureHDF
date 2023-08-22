namespace PureHDF.VOL.Native;

internal partial record class AttributeInfoMessage
{
    public override ushort GetEncodeSize()
    {
        var size = 
            sizeof(byte) +
            sizeof(byte) +
            (
                Flags.HasFlag(CreationOrderFlags.TrackCreationOrder)
                    ? sizeof(ushort)
                    : 0
            ) +
            sizeof(ulong) +
            sizeof(ulong) +
            (
                Flags.HasFlag(CreationOrderFlags.IndexCreationOrder)
                    ? sizeof(ulong)
                    : 0
            );

        return (ushort)size;
    }

    public override void Encode(H5DriverBase driver)
    {
        // version
        driver.Write(Version);

        // flags
        driver.Write((byte)Flags);

        // maximum creation index
        if (Flags.HasFlag(CreationOrderFlags.TrackCreationOrder))
            driver.Write(MaximumCreationIndex);

        // fractal heap address
        driver.Write(FractalHeapAddress);

        // b-tree 2 name index address
        driver.Write(BTree2NameIndexAddress);

        // b-tree 2 creation order index address
        if (Flags.HasFlag(CreationOrderFlags.IndexCreationOrder))
            driver.Write(BTree2CreationOrderIndexAddress);
    }
}