using System.IO;

namespace HDF5.NET
{
    public class HugeObjectsFractalHeapIdSubType1And2 : FractalHeapId
    {
        #region Constructors

        public HugeObjectsFractalHeapIdSubType1And2(BinaryReader reader, Superblock superblock) : base(reader)
        {
            // BTree2 key
            this.BTree2Key = superblock.ReadLength();
        }

        #endregion

        #region Properties

        public ulong BTree2Key { get; set; }

        protected override FractalHeapIdType ExpectedType => FractalHeapIdType.Huge;

        #endregion
    }
}
