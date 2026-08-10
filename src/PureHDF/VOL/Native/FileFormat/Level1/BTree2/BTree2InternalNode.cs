using System.Text;

namespace PureHDF.VOL.Native;

internal record class BTree2InternalNode<T>(
    T[] Records,
    BTree2NodePointer[] NodePointers
) : BTree2Node<T>(Records) where T : struct, IBTree2Record
{
    public static async ValueTask<BTree2InternalNode<T>> Decode(
        NativeReadContext context,
        BTree2Header<T> header,
        ulong recordCount,
        int nodeLevel,
        Func<ValueTask<T>> decodeKey)
    {
        var (driver, superblock) = context;

        var (version, records) = await Decode(
            driver,
            header,
            recordCount,
            Signature,
            decodeKey
        ).ConfigureAwait(false);

        var nodePointers = new BTree2NodePointer[recordCount + 1];

        // H5B2cache.c (H5B2__cache_int_deserialize)
        for (ulong i = 0; i < recordCount + 1; i++)
        {
            // address
            var address = await superblock.ReadOffset(driver).ConfigureAwait(false);

            // record count
            var childRecordCount = await ReadUtils.ReadUlong(driver, header.MaxRecordCountSize).ConfigureAwait(false);

            // total record count
            ulong totalRecordCount;

            if (nodeLevel > 1)
            {
                var totalChildRecordCount = await ReadUtils.ReadUlong(driver, header.NodeInfos[nodeLevel - 1].CumulatedTotalRecordCountSize).ConfigureAwait(false);
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
        var _ = await driver.ReadUInt32().ConfigureAwait(false);

        return new BTree2InternalNode<T>(
            records,
            nodePointers)
        {
            Version = version
        };
    }

    public static byte[] Signature { get; } = Encoding.ASCII.GetBytes("BTIN");
}