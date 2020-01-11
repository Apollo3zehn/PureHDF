using System.Collections.Generic;
using System.IO;
using System.Text;

namespace HDF5.NET
{
    public class SharedObjectHeaderMessageTable : FileBlock
    {
        #region Constructors

        public SharedObjectHeaderMessageTable(BinaryReader reader) : base(reader)
        {
            // signature
            var signature = reader.ReadBytes(4);
            H5Utils.ValidateSignature(signature, SharedObjectHeaderMessageTable.Signature);

            //
#warning implement this correctly

            // checksum
            this.Checksum = reader.ReadUInt32();
        }

        #endregion

        #region Properties

        public static byte[] Signature { get; } = Encoding.ASCII.GetBytes("SMTB");

        public List<byte> Versions { get; set; }
        public List<MessageTypeFlags> MessageTypeFlags { get; set; }
        public List<uint> MinimumMessageSize { get; set; }
        public List<ushort> ListCutoff { get; set; }
        public List<ushort> BTree2Cutoff { get; set; }
        public List<ushort> MessageCount { get; set; }
        public List<ulong> IndexAddress { get; set; }
        public List<ulong> FractalHeapAddress { get; set; }
        public uint Checksum { get; set; }

        #endregion
    }
}
