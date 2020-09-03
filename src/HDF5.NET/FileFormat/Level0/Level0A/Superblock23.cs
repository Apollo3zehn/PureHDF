using System.IO;

namespace HDF5.NET
{
    public class Superblock23 : Superblock
    {
        #region Constructors

        public Superblock23(BinaryReader reader, byte version) : base(reader)
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

        public ulong BaseAddress { get; set; }
        public ulong SuperblockExtensionAddress { get; set; }
        public ulong EndOfFileAddress { get; set; }
        public ulong RootGroupObjectHeaderAddress { get; set; }
        public uint SuperblockChecksum { get; set; }

        public ObjectHeader? SuperblockExtension
        {
            get
            {
                if (this.IsUndefinedAddress(this.SuperblockExtensionAddress))
                {
                    return null;
                }
                else
                {
                    this.Reader.BaseStream.Seek((long)this.SuperblockExtensionAddress, SeekOrigin.Begin);
                    return ObjectHeader.Construct(this.Reader, this);
                }
            }
        }

        public ObjectHeader RootGroupObjectHeader
        {
            get
            {
                this.Reader.BaseStream.Seek((long)this.RootGroupObjectHeaderAddress, SeekOrigin.Begin);
                return ObjectHeader.Construct(this.Reader, this);
            }
        }

        #endregion
    }
}
