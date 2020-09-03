using System.IO;

namespace HDF5.NET
{
    public class HardLinkInfo : LinkInfo
    {
        #region Fields

#warning OK like this?
        private Superblock _superblock;

        #endregion

        #region Constructors

        public HardLinkInfo(BinaryReader reader, Superblock superblock) : base(reader)
        {
            _superblock = superblock;

            // object header address
            this.ObjectHeaderAddress = superblock.ReadOffset(reader);
        }

        #endregion

        #region Properties

        public ulong ObjectHeaderAddress { get; set; }

        public ObjectHeader ObjectHeader
        {
            get
            {
                this.Reader.BaseStream.Seek((long)this.ObjectHeaderAddress, SeekOrigin.Begin);
                return ObjectHeader.Construct(this.Reader, _superblock);
            }
        }

        #endregion
    }
}
