using System.IO;

namespace HDF5.NET
{
    public class ObjectHeaderScratchPad : ScratchPad
    {
        #region Constructors

        public ObjectHeaderScratchPad(BinaryReader reader, Superblock superblock) : base(reader)
        {
            this.BTreeAddress = superblock.ReadLength();
            this.NameHeapAddress = superblock.ReadLength();
        }

        #endregion

        #region Properties

        public ulong BTreeAddress { get; set; }
        public ulong NameHeapAddress { get; set; }

        #endregion
    }
}
