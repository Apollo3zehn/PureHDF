namespace PureHDF.VOL.Native;

internal abstract record class BTree2Node<T>(
    T[] Records
) where T : struct, IBTree2Record
{
    private byte _version;

    // The version and records come back in a tuple rather than through `out` parameters, which
    // cannot coexist with `async` (CS1988).
    public static async ValueTask<(byte Version, T[] Records)> Decode(
        NativeReadContext context,
        BTree2Header<T> header,
        ulong recordCount,
        byte[] signature,
        DecodeKeyDelegate<T> decodeKey)
    {
        var driver = context.Driver;

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
            records[i] = await decodeKey(context).ConfigureAwait(false);
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