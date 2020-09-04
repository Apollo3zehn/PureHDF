using System.IO;

namespace HDF5.NET
{
    public class HugeObjectsFractalHeapIdSubType1 : FractalHeapId
    {
        #region Constructors

        public HugeObjectsFractalHeapIdSubType1(BinaryReader reader, FractalHeapHeader header) : base(reader)
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
