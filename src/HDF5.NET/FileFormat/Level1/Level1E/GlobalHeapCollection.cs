using System.Collections.Generic;

namespace HDF5.NET
{
    public class GlobalHeapCollection
    {
        #region Constructors

        public GlobalHeapCollection()
        {
            //
        }

        #endregion

        #region Properties

        public byte[] Signature { get; set; }
        public byte Version { get; set; }
        public ulong CollectionSize { get; set; }
        public List<GlobalHeapObject> GlobalHeapObjects { get; set; }

        #endregion
    }
}
