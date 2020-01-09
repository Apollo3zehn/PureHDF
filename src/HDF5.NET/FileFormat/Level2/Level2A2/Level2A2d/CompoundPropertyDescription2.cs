using System.IO;

namespace HDF5.NET
{
    public class CompoundPropertyDescription2 : DatatypePropertyDescription
    {
        #region Constructors

        public CompoundPropertyDescription2(BinaryReader reader) : base(reader)
        {
            // name
            this.Name = H5Utils.ReadNullTerminatedString(reader, pad: true);

            // member byte offset
            this.MemberByteOffset = reader.ReadUInt32();

            // member type message
            this.MemberTypeMessage = new DatatypeMessage(reader);
        }

        #endregion

        #region Properties

        public string Name { get; set; }
        public uint MemberByteOffset { get; set; }
        public DatatypeMessage MemberTypeMessage { get; set; }

        #endregion
    }
}