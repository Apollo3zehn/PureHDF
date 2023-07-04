﻿namespace PureHDF.VOL.Native;

internal partial record class LinkInfoMessage
{
    public override void Encode(BinaryWriter driver)
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

        // BTree2 name index address
        driver.Write(BTree2NameIndexAddress);

        // BTree2 creation order index address
        if (Flags.HasFlag(CreationOrderFlags.IndexCreationOrder))
            driver.Write(BTree2CreationOrderIndexAddress);
    }
}