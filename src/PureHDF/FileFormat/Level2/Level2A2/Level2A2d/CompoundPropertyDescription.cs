namespace PureHDF
{
    internal class CompoundPropertyDescription : DatatypePropertyDescription
    {
        #region Constructors

        public CompoundPropertyDescription(H5BaseReader reader, byte version, uint valueSize)
        {
            switch (version)
            {
                case 1:

                    // name
                    Name = H5ReadUtils.ReadNullTerminatedString(reader, pad: true);

                    // member byte offset
                    MemberByteOffset = reader.ReadUInt32();

                    // rank
                    _ = reader.ReadByte();

                    // padding bytes
                    reader.ReadBytes(3);

                    // dimension permutation
                    _ = reader.ReadUInt32();

                    // padding byte
                    reader.ReadBytes(4);

                    // dimension sizes
                    var dimensionSizes = new uint[4];

                    for (int i = 0; i < 4; i++)
                    {
                        dimensionSizes[i] = reader.ReadUInt32();
                    }

                    // member type message
                    MemberTypeMessage = new DatatypeMessage(reader);

                    break;

                case 2:

                    // name
                    Name = H5ReadUtils.ReadNullTerminatedString(reader, pad: true);

                    // member byte offset
                    MemberByteOffset = reader.ReadUInt32();

                    // member type message
                    MemberTypeMessage = new DatatypeMessage(reader);

                    break;

                case 3:

                    // name
                    Name = H5ReadUtils.ReadNullTerminatedString(reader, pad: false);

                    // member byte offset
                    var byteCount = Utils.FindMinByteCount(valueSize);

                    if (!(1 <= byteCount && byteCount <= 8))
                        throw new NotSupportedException("A compound property description member byte offset byte count must be within the range of 1..8.");

                    var buffer = new byte[8];

                    for (ulong i = 0; i < byteCount; i++)
                    {
                        buffer[i] = reader.ReadByte();
                    }

                    MemberByteOffset = BitConverter.ToUInt64(buffer, 0);

                    // member type message
                    MemberTypeMessage = new DatatypeMessage(reader);

                    break;

                default:
                    throw new Exception("The version parameter must be in the range 1..3.");
            }
        }

        #endregion

        #region Properties

        public string Name { get; set; }
        public ulong MemberByteOffset { get; set; }
        public DatatypeMessage MemberTypeMessage { get; set; }

        #endregion
    }
}