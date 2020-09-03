using System.IO;

namespace HDF5.NET
{
    public class HugeObjectsFractalHeapIdSubType3 : FractalHeapId
    {
        #region Constructors

        public HugeObjectsFractalHeapIdSubType3(BinaryReader reader, Superblock superblock) : base(reader)
        {
            // address
            this.Address = superblock.ReadOffset(reader);

            // length
            this.Length = superblock.ReadLength(reader);
        }

        #endregion

        #region Properties

        public ulong Address { get; set; }
        public ulong Length { get; set; }

        #endregion
    }
}
