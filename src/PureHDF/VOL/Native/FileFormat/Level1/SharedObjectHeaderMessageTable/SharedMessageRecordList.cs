using System.Text;

namespace PureHDF.VOL.Native;

internal readonly record struct SharedMessageRecordList(
    List<SharedMessageRecord> SharedMessageRecords
)
{
    public static byte[] Signature { get; } = Encoding.ASCII.GetBytes("SMLI");

    public static async ValueTask<SharedMessageRecordList> Decode(H5DriverBase driver)
    {
        // signature
        var signature = await driver.ReadBytes(4).ConfigureAwait(false);
        MathUtils.ValidateSignature(signature, Signature);

        // share message records
        var sharedMessageRecords = new List<SharedMessageRecord>();
        // TODO: how to know how many?

        // checksum
        var _ = await driver.ReadUInt32().ConfigureAwait(false);

        return new SharedMessageRecordList(
            sharedMessageRecords
        );
    }
}