using System.Text;

namespace HDF5.NET
{
    internal class SharedMessageRecordList
    {
        #region Constructors

        public SharedMessageRecordList(H5BaseReader reader)
        {
            // signature
            var signature = reader.ReadBytes(4);
            H5Utils.ValidateSignature(signature, SharedMessageRecordList.Signature);

            // share message records
            SharedMessageRecords = new List<SharedMessageRecord>();
            // TODO: how to know how many?

            // checksum
            Checksum = reader.ReadUInt32();
        }

        #endregion

        #region Properties

        public static byte[] Signature { get; } = Encoding.ASCII.GetBytes("SMLI");

        public List<SharedMessageRecord> SharedMessageRecords { get; set; }
        public uint Checksum { get; set; }

        #endregion
    }
}
