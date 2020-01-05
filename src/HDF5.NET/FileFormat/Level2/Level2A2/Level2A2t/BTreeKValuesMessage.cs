using System.IO;

namespace HDF5.NET
{
    public class BTreeKValuesMessage : Message
    {
        #region Constructors

        public BTreeKValuesMessage(BinaryReader reader) : base(reader)
        {
            //
        }

        #endregion

        #region Properties

        public byte Version { get; set; }
        public ushort IndexedStorageInternalNodeK { get; set; }
        public ushort GroupInternalNodeK { get; set; }
        public ushort GroupLeafNodeK { get; set; }

        #endregion
    }
}
