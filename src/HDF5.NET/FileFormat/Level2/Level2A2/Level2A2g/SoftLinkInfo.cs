using System.IO;

namespace HDF5.NET
{
    public class SoftLinkInfo : LinkInfo
    {
        #region Constructors

        public SoftLinkInfo(BinaryReader reader) : base(reader)
        {
            // value length
            this.ValueLength = reader.ReadUInt16();

            // value
            this.Value = H5Utils.ReadFixedLengthString(reader, this.ValueLength);
        }

        #endregion

        #region Properties

        public ushort ValueLength { get; set; }
        public string Value { get; set; }

        #endregion
    }
}
