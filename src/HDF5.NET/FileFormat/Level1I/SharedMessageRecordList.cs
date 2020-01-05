using System.Collections.Generic;

namespace HDF5.NET
{
    public class SharedMessageRecordList
    {
        #region Constructors

        public SharedMessageRecordList()
        {
            //
        }

        #endregion

        #region Properties

        public byte[] Signature { get; set; }
        public List<SharedMessageRecord> SharedMessageRecords { get; set; }
        public uint Checksum { get; set; }

        #endregion
    }
}
