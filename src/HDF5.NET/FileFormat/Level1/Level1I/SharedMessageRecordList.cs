using System.Collections.Generic;
using System.IO;
using System.Text;

namespace HDF5.NET
{
    public class SharedMessageRecordList : FileBlock
    {
        #region Constructors

        public SharedMessageRecordList(BinaryReader reader) : base(reader)
        {
            // signature
            var signature = reader.ReadBytes(4);
            H5Utils.ValidateSignature(signature, SharedMessageRecordList.Signature);

            // share message records
            this.SharedMessageRecords = new List<SharedMessageRecord>();
#warning how to know how many?

            // checksum
            this.Checksum = reader.ReadUInt32();
        }

        #endregion

        #region Properties

        public static byte[] Signature { get; } = Encoding.ASCII.GetBytes("SMLI");

        public List<SharedMessageRecord> SharedMessageRecords { get; set; }
        public uint Checksum { get; set; }

        #endregion
    }
}
