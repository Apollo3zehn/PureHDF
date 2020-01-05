using System.IO;
using System.Text;

namespace HDF5.NET
{
    public class LocalHeap : FileBlock
    {
        #region Constructors

        public LocalHeap(BinaryReader reader, Superblock superblock) : base(reader)
        {
            var signature = reader.ReadBytes(4);
            this.ValidateSignature(signature, LocalHeap.Signature);

            this.Version = reader.ReadByte();
            reader.ReadBytes(3);

            this.DataSegmentSize = superblock.ReadLength();
            this.FreeListHeadOffset = superblock.ReadLength();
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
