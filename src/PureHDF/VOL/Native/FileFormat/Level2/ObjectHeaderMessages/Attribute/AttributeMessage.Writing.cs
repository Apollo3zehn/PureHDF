﻿using System.Text;

namespace PureHDF.VOL.Native;

internal partial record class AttributeMessage
{
    public override ushort GetEncodeSize()
    {
        if (Version != 3)
            throw new Exception("Only version 3 attribute messages are supported.");

        var nameEncodeSize = Encoding.UTF8.GetBytes(Name).Length + 1;
        var dataSize = Datatype.Size * Dataspace.DimensionSizes.Aggregate(1UL, (product, dimension) => product * dimension);

        // TODO: make this more exact?
        if (dataSize > 64 * 1024)
            throw new Exception("The maximum attribute size is 64KB.");

        var size =
            sizeof(byte) +
            sizeof(byte) +
            sizeof(ushort) +
            sizeof(ushort) +
            sizeof(ushort) +
            sizeof(byte) +
            nameEncodeSize +
            Datatype.GetEncodeSize() +
            Dataspace.GetEncodeSize() +
            (ushort)dataSize;

        return (ushort)size;
    }

    public override void Encode(BinaryWriter driver)
    {
        // version
        driver.Write(Version);

        // flags
        if (Version == 1)
            driver.Seek(1, SeekOrigin.Current);

        else
            driver.Write((byte)Flags);

        // name size
        var nameBytes = Encoding.UTF8.GetBytes(Name);
        driver.Write((ushort)(nameBytes.Length + 1));

        // datatype size
        var dataTypeEncodeSize = Datatype.GetEncodeSize();
        driver.Write(dataTypeEncodeSize);

        // dataspace size
        var dataSpaceEncodeSize = Dataspace.GetEncodeSize();
        driver.Write(dataSpaceEncodeSize);

        // name character set encoding
        if (Version == 3)
            driver.Write((byte)CharacterSetEncoding.UTF8);

        // name
        if (Version == 1)
        {
            throw new NotImplementedException() /* Version 1 requires padding */;
        }

        else
        {
            driver.Write(nameBytes);
            driver.Write((byte)0);
        }

        // datatype
        Datatype.Encode(driver);

        if (Version == 1)
            throw new NotImplementedException() /* Version 1 requires padding */;

        // dataspace
        Dataspace.Encode(driver);

        if (Version == 1)
            throw new NotImplementedException() /* Version 1 requires padding */;

        // data
        EncodeData.Invoke(driver);
    }
}