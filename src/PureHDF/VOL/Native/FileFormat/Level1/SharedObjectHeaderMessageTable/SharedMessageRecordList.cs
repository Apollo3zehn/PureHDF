using System.Text;

namespace PureHDF.VOL.Native;

internal readonly record struct SharedMessageRecordList(
    List<SharedMessageRecord> SharedMessageRecords
)
{
    public static byte[] Signature { get; } = Encoding.ASCII.GetBytes("SMLI");

    public static SharedMessageRecordList Decode(H5DriverBase driver)
    {
        // signature
        var signature = driver.ReadBytes(4);
        MathUtils.ValidateSignature(signature, Signature);

        // share message records
        var sharedMessageRecords = new List<SharedMessageRecord>();
        // TODO: how to know how many?

        // checksum
        var _ = driver.ReadUInt32();

        return new SharedMessageRecordList(
            sharedMessageRecords
        );
    }
}