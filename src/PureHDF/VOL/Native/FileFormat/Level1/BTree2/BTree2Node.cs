namespace PureHDF.VOL.Native;

internal abstract record class BTree2Node<T>(
    T[] Records
) where T : struct, IBTree2Record
{
    private byte _version;

    // NOTE (async propagation): `out byte version, out T[] records` cannot coexist with
    // `async` (CS1988), so both out parameters became a tuple return. Callers outside this
    // file (BTree2InternalNode.cs, BTree2LeafNode.cs) need updating — see report.
    public static async ValueTask<(byte Version, T[] Records)> Decode(
        H5DriverBase driver,
        BTree2Header<T> header,
        ulong recordCount,
        byte[] signature,
        Func<ValueTask<T>> decodeKey)
    {
        // signature
        var actualSignature = await driver.ReadBytes(4).ConfigureAwait(false);
        MathUtils.ValidateSignature(actualSignature, signature);

        // version
        var version = await driver.ReadByte().ConfigureAwait(false);

        // type
        var type = (BTree2Type)(await driver.ReadByte().ConfigureAwait(false));

        if (type != header.Type)
            throw new FormatException($"The BTree2 internal node type '{type}' does not match the type defined in the header '{header.Type}'.");

        // records
        var records = new T[recordCount];

        for (var i = 0UL; i < recordCount; i++)
        {
            records[i] = await decodeKey().ConfigureAwait(false);
        }

        return (version, records);
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