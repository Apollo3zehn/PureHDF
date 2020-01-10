using System.IO;
using System.Text;

namespace HDF5.NET
{
    public class LocalHeap : FileBlock
    {
        #region Constructors

        public LocalHeap(BinaryReader reader, Superblock superblock) : base(reader)
        {
            // signature
            var signature = reader.ReadBytes(4);
            H5Utils.ValidateSignature(signature, LocalHeap.Signature);

            // version
            this.Version = reader.ReadByte();

            // reserved
            reader.ReadBytes(3);

            // data segment size
            this.DataSegmentSize = superblock.ReadLength();

            // free list head offset
            this.FreeListHeadOffset = superblock.ReadLength();

            // data segment address
            this.DataSegmentAddress = superblock.ReadOffset();
        }

        #endregion

        #region Properties

        public static byte[] Signature { get; set; } = Encoding.ASCII.GetBytes("HEAP");

        public byte Version { get; set; }
        public ulong DataSegmentSize { get; set; }
        public ulong FreeListHeadOffset { get; set; }
        public ulong DataSegmentAddress { get; set; }

        #endregion
    }
}
