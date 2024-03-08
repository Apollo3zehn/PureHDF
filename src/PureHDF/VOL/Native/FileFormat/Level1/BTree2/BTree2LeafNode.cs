using System.Text;

namespace PureHDF.VOL.Native;

internal record class BTree2LeafNode<T>(
    T[] Records
) : BTree2Node<T>(Records) where T : struct, IBTree2Record
{
    public static BTree2LeafNode<T> Decode(
        H5DriverBase driver,
        BTree2Header<T> header,
        ulong recordCount,
        Func<T> decodeKey)
    {
        Decode(
            driver,
            header,
            recordCount,
            Signature,
            decodeKey,
            out var version,
            out var records
        );

        // checksum
        var _ = driver.ReadUInt32();

        return new BTree2LeafNode<T>(records)
        {
            Version = version
        };
    }

    public static byte[] Signature { get; } = Encoding.ASCII.GetBytes("BTLF");
}