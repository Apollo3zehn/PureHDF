using System.Text;

namespace PureHDF.VOL.Native;

internal partial record class AttributeMessage
{
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
        var position1 = driver.BaseStream.Position;
        driver.Write((ushort)0 /* dummy */);

        // dataspace size
        driver.Write((ushort)0 /* dummy */);

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
        var position2 = driver.BaseStream.Position;
        Datatype.Encode(driver);
        var position3 = driver.BaseStream.Position;

        if (Version == 1)
            throw new NotImplementedException() /* Version 1 requires padding */;

        // dataspace
        var position4 = driver.BaseStream.Position;
        Dataspace.Encode(driver);
        var position5 = driver.BaseStream.Position;

        if (Version == 1)
            throw new NotImplementedException() /* Version 1 requires padding */;

        // data
#if NETSTANDARD2_1_OR_GREATER
        driver.Write(Data.Span);
#else
        driver.Write(Data.Span.ToArray());
#endif

        var position6 = driver.BaseStream.Position;

        // TODO make this more efficient
        // datatype size & dataspace size
        driver.BaseStream.Seek(position1, SeekOrigin.Begin);
        driver.Write((ushort)(position3 - position2));
        driver.Write((ushort)(position5 - position4));
        driver.BaseStream.Seek(position6, SeekOrigin.Begin);
    }
}