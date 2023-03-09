using System.Text;

namespace PureHDF
{
    internal class SharedMessageRecordList
    {
        #region Constructors

        public SharedMessageRecordList(H5DriverBase driver)
        {
            // signature
            var signature = driver.ReadBytes(4);
            Utils.ValidateSignature(signature, Signature);

            // share message records
            SharedMessageRecords = new List<SharedMessageRecord>();
            // TODO: how to know how many?

            // checksum
            Checksum = driver.ReadUInt32();
        }

        #endregion

        #region Properties

        public static byte[] Signature { get; } = Encoding.ASCII.GetBytes("SMLI");

        public List<SharedMessageRecord> SharedMessageRecords { get; set; }
        public uint Checksum { get; set; }

        #endregion
    }
}
