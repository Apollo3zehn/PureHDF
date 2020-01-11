using System.IO;

namespace HDF5.NET
{
    public class ObjectHeaderContinuationMessage : Message
    {
        #region Constructors

        public ObjectHeaderContinuationMessage(BinaryReader reader, Superblock superblock) : base(reader)
        {
            this.Offset = superblock.ReadOffset();
            this.Length = superblock.ReadLength();
        }

        #endregion

        #region Properties

        public ulong Offset { get; set; }
        public ulong Length { get; set; }

        public ObjectHeaderContinuationBlock2 ObjectHeaderContinuationBlock
        {
            get
            {
                this.Reader.BaseStream.Seek((long)this.Offset, SeekOrigin.Begin);
#warning What if this is a version 1 ObjectHeaderContinuationBlock?
                return new ObjectHeaderContinuationBlock2(this.Reader);
            }
        }

        #endregion
    }
}
