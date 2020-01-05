using System.Collections.Generic;

namespace HDF5.NET
{
    public class FreeSpaceSectionList
    {
        #region Constructors

        public FreeSpaceSectionList()
        {
            //
        }

        #endregion

        #region Properties

        public byte[] Signature { get; set; }
        public byte Version { get; set; }
        public ulong FreeSpaceManagerHeaderAddress { get; set; }
        public List<ulong> SectionRecordsCount { get; set; }
        public List<ulong> FreeSpaceSectionSize { get; set; }
        public List<ulong> SectionRecordOffset { get; set; } // actually it is a List<List<ulong>>
        public List<ulong> SectionRecordType { get; set; } // actually it is a List<List<SectionType>>
        public List<SectionDataRecord> SectionRecordData { get; set; } // actually it is a List<List<SectionDataRecord>>



        #endregion
    }
}
