using System.Text;

namespace PureHDF.VOL.Native;

internal record class BTree2InternalNode<T>(
    T[] Records,
    BTree2NodePointer[] NodePointers
) : BTree2Node<T>(Records) where T : struct, IBTree2Record
{
    public static BTree2InternalNode<T> Decode(
        NativeContext context, 
        BTree2Header<T> header, 
        ulong recordCount, 
        int nodeLevel, 
        Func<T> decodeKey)
    {
        var (driver, superblock) = context;

        Decode(
            driver, 
            header, 
            recordCount, 
            Signature, 
            decodeKey, 
            out var version,
            out var records
        );

        var nodePointers = new BTree2NodePointer[recordCount + 1];

        // H5B2cache.c (H5B2__cache_int_deserialize)
        for (ulong i = 0; i < recordCount + 1; i++)
        {
            // address
            var address = superblock.ReadOffset(driver);

            // record count
            var childRecordCount = Utils.ReadUlong(driver, header.MaxRecordCountSize);

            // total record count
            ulong totalRecordCount;

            if (nodeLevel > 1)
            {
                var totalChildRecordCount = Utils.ReadUlong(driver, header.NodeInfos[nodeLevel - 1].CumulatedTotalRecordCountSize);
                totalRecordCount = totalChildRecordCount;
            }

            else
            {
                totalRecordCount = childRecordCount;
            }

            nodePointers[i] = new BTree2NodePointer(
                address, 
                childRecordCount, 
                totalRecordCount);
        }

        // checksum
        var _ = driver.ReadUInt32();

        return new BTree2InternalNode<T>(
            records,
            nodePointers)
        {
            Version = version
        };
    }

    public static byte[] Signature { get; } = Encoding.ASCII.GetBytes("BTIN");
}