namespace HDF5.NET
{
    public class Superblock23 : Superblock
    {
        #region Constructors

        public Superblock23(H5BinaryReader reader, byte version) : base(reader)
        {
            this.SuperBlockVersion = version;
            this.OffsetsSize = reader.ReadByte();
            this.LengthsSize = reader.ReadByte();
            this.FileConsistencyFlags = (FileConsistencyFlags)reader.ReadByte();
            this.BaseAddress = this.ReadOffset(reader);
            this.SuperblockExtensionAddress = this.ReadOffset(reader);
            this.EndOfFileAddress = this.ReadOffset(reader);
            this.RootGroupObjectHeaderAddress = this.ReadOffset(reader);
            this.SuperblockChecksum = reader.ReadUInt32();
        }

        #endregion

        #region Properties

        public ulong SuperblockExtensionAddress { get; set; }
        public ulong RootGroupObjectHeaderAddress { get; set; }
        public uint SuperblockChecksum { get; set; }

        #endregion
    }
}
