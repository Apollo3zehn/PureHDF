namespace PureHDF.VOL.Native;

internal abstract class BTree2Node<T> where T : struct, IBTree2Record
{
    #region Fields

    private byte _version;

    #endregion

    public BTree2Node(H5DriverBase driver, BTree2Header<T> header, ushort recordCount, byte[] signature, Func<T> decodeKey)
    {
        // signature
        var actualSignature = driver.ReadBytes(4);
        Utils.ValidateSignature(actualSignature, signature);

        // version
        Version = driver.ReadByte();

        // type
        Type = (BTree2Type)driver.ReadByte();

        if (Type != header.Type)
            throw new FormatException($"The BTree2 internal node type ('{Type}') does not match the type defined in the header ('{header.Type}').");

        // records
        Records = new T[recordCount];

        for (ulong i = 0; i < recordCount; i++)
        {
            Records[i] = decodeKey();
        }
    }

    #region Properties

    public byte Version
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

    public BTree2Type Type { get; }
    public T[] Records { get; }
    public uint Checksum { get; protected set; }

    #endregion
}