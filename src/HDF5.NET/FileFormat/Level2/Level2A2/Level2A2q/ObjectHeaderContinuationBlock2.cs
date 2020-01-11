using System.Collections.Generic;
using System.IO;
using System.Text;

namespace HDF5.NET
{
    public class ObjectHeaderContinuationBlock2 : FileBlock
    {
        #region Constructors

        public ObjectHeaderContinuationBlock2(BinaryReader reader) : base(reader)
        {
            // signature
            var signature = reader.ReadBytes(4);
            H5Utils.ValidateSignature(signature, BTree2LeafNode.Signature);

#warning Parse also remaining parts


            // checksum
            this.Checksum = reader.ReadUInt32();
        }

        #endregion

        #region Properties

        public static byte[] Signature { get; } = Encoding.ASCII.GetBytes("OCHK");

        public List<HeaderMessageType> HeaderMessageTypes { get; set; }
        public List<ushort> HeaderMessageDataSizes { get; set; }
        public List<HeaderMessageFlags> HeaderMessageFlags { get; set; }
        public List<ushort> HeaderMessageCreationOrder { get; set; }
        public List<byte[]> HeaderMessageData { get; set; }
        public uint Checksum { get; set; }

        #endregion
    }
}
