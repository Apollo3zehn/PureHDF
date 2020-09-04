using System.IO;

namespace HDF5.NET
{
    public class Superblock01 : Superblock
    {
        #region Constructors

        public Superblock01(BinaryReader reader, byte version) : base(reader)
        {
            this.SuperBlockVersion = version;
            this.FreeSpaceStorageVersion = reader.ReadByte();
            this.RootGroupSymbolTableEntryVersion = reader.ReadByte();
            reader.ReadByte();

            this.SharedHeaderMessageFormatVersion = reader.ReadByte();
            this.OffsetsSize = reader.ReadByte();
            this.LengthsSize = reader.ReadByte();
            reader.ReadByte();

            this.GroupLeafNodeK = reader.ReadUInt16();
            this.GroupInternalNodeK = reader.ReadUInt16();

            this.FileConsistencyFlags = (FileConsistencyFlags)reader.ReadUInt32();

            if (this.SuperBlockVersion == 1)
            {
                this.IndexedStorageInternalNodeK = reader.ReadUInt16();
                reader.ReadUInt16();
            }

            this.BaseAddress = this.ReadOffset(reader);
            this.FreeSpaceInfoAddress = this.ReadOffset(reader);
            this.EndOfFileAddress = this.ReadOffset(reader);
            this.DriverInfoBlockAddress = this.ReadOffset(reader);
            this.RootGroupSymbolTableEntry = new SymbolTableEntry(reader, this);
        }

        #endregion

        #region Properties

        public byte FreeSpaceStorageVersion { get; set; }
        public byte RootGroupSymbolTableEntryVersion { get; set; }
        public byte SharedHeaderMessageFormatVersion { get; set; }
        public ushort GroupLeafNodeK { get; set; }
        public ushort GroupInternalNodeK { get; set; }
        public ushort IndexedStorageInternalNodeK { get; set; }
        public ulong BaseAddress { get; set; }
        public ulong FreeSpaceInfoAddress { get; set; }
        public ulong EndOfFileAddress { get; set; }
        public ulong DriverInfoBlockAddress { get; set; }
        public SymbolTableEntry RootGroupSymbolTableEntry { get; set; }

        #endregion

        #region Properties

        public DriverInfoBlock? DriverInfoBlock
        {
            get
            {
                if (this.IsUndefinedAddress(this.DriverInfoBlockAddress))
                    return null;
                else
                    return new DriverInfoBlock(this.Reader);
            }
        }

        #endregion
    }
}
