using System.IO;

namespace HDF5.NET
{
    public class HugeObjectsFractalHeapIdSubType3 : FractalHeapId
    {
        #region Constructors

        public HugeObjectsFractalHeapIdSubType3(BinaryReader reader, Superblock superblock) : base(reader)
        {
            // address
            this.Address = superblock.ReadOffset();

            // length
            this.Length = superblock.ReadLength();
        }

        #endregion

        #region Properties

        public ulong Address { get; set; }
        public ulong Length { get; set; }

        protected override FractalHeapIdType ExpectedType => FractalHeapIdType.Huge;

        #endregion
    }
}
