using Microsoft.Extensions.Logging;
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

            this.BaseAddress = this.ReadOffset();
            this.FreeSpaceInfoAddress = this.ReadOffset();
            this.EndOfFileAddress = this.ReadOffset();
            this.DriverInformationBlockAddress = this.ReadOffset();
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
        public ulong DriverInformationBlockAddress { get; set; }
        public SymbolTableEntry RootGroupSymbolTableEntry { get; set; }

        #endregion

        #region Methods

        public override void Print(ILogger logger)
        {
            logger.LogInformation("Superblock");
            logger.LogInformation($"Superblock GroupLeafNodeK: {this.GroupLeafNodeK}");
            logger.LogInformation($"Superblock GroupInternalNodeK: {this.GroupInternalNodeK}");
            logger.LogInformation($"Superblock IndexedStorageInternalNodeK: {this.IndexedStorageInternalNodeK}");

            this.RootGroupSymbolTableEntry.Print(logger);
        }

        #endregion
    }
}
