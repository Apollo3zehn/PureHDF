using System.Text;

namespace PureHDF
{
    internal class SharedObjectHeaderMessageTable
    {
        #region Constructors

        public SharedObjectHeaderMessageTable(H5DriverBase driver)
        {
            // signature
            var signature = driver.ReadBytes(4);
            Utils.ValidateSignature(signature, Signature);

            //
            // TODO: implement this correctly

            // checksum
            Checksum = driver.ReadUInt32();
        }

        #endregion

        #region Properties

        public static byte[] Signature { get; } = Encoding.ASCII.GetBytes("SMTB");

        // TODO: implement this correctly
        // public List<byte> Versions { get; set; }
        // public List<MessageTypeFlags> MessageTypeFlags { get; set; }
        // public List<uint> MinimumMessageSize { get; set; }
        // public List<ushort> ListCutoff { get; set; }
        // public List<ushort> BTree2Cutoff { get; set; }
        // public List<ushort> MessageCount { get; set; }
        // public List<ulong> IndexAddress { get; set; }
        // public List<ulong> FractalHeapAddress { get; set; }
        public uint Checksum { get; set; }

        #endregion
    }
}
