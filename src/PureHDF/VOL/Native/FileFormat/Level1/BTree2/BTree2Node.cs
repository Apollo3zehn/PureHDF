namespace PureHDF.VOL.Native;

internal abstract record class BTree2Node<T>(
    T[] Records
) where T : struct, IBTree2Record
{
    private byte _version;

    public static void Decode(
        H5DriverBase driver, 
        BTree2Header<T> header, 
        ulong recordCount, 
        byte[] signature, 
        Func<T> decodeKey,
        out byte version,
        out T[] records)
    {
        // signature
        var actualSignature = driver.ReadBytes(4);
        Utils.ValidateSignature(actualSignature, signature);

        // version
        version = driver.ReadByte();

        // type
        var type = (BTree2Type)driver.ReadByte();

        if (type != header.Type)
            throw new FormatException($"The BTree2 internal node type '{type}' does not match the type defined in the header '{header.Type}'.");

        // records
        records = new T[recordCount];

        for (var i = 0UL; i < recordCount; i++)
        {
            records[i] = decodeKey();
        }
    }

    public required byte Version
    {
        get
        {
            return _version;
        }
        set
        {
            if (value != 0)
                throw new FormatException($"Only version 0 instances of type {nameof(BTree2Node<T>)} are supported.");

            _version = value;
        }
    }
}