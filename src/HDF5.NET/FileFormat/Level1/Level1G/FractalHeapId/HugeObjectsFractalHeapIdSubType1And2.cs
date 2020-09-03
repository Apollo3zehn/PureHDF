using System.IO;

namespace HDF5.NET
{
    public class HugeObjectsFractalHeapIdSubType1And2 : FractalHeapId
    {
        #region Constructors

        public HugeObjectsFractalHeapIdSubType1And2(BinaryReader reader, FractalHeapHeader header) : base(reader)
        {
            // BTree2 key
            this.BTree2Key = H5Utils.ReadUlong(reader, header.HugeIdsSize);
        }

        #endregion

        #region Properties

        public ulong BTree2Key { get; set; }

        #endregion
    }
}
