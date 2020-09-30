using System;

namespace HDF5.NET
{
    public class SymbolTableEntry : FileBlock
    {
        #region Fields

        Superblock _superblock;

        #endregion

        #region Constructors

        public SymbolTableEntry(H5BinaryReader reader, Superblock superblock) : base(reader)
        {
            _superblock = superblock;

            // link name offset
            this.LinkNameOffset = superblock.ReadOffset(reader);
            
            // object header address
            this.HeaderAddress = superblock.ReadOffset(reader);

            // cache type
            this.CacheType = (CacheType)reader.ReadUInt32();

            // reserved
            reader.ReadUInt32();

            // scratch pad
            var before = reader.BaseStream.Position;

            this.ScratchPad = this.CacheType switch
            {
                CacheType.NoCache => null,
                CacheType.ObjectHeader => new ObjectHeaderScratchPad(reader, superblock),
                CacheType.SymbolicLink => new SymbolicLinkScratchPad(reader),
                _ => throw new NotSupportedException()
            };

            var after = reader.BaseStream.Position;
            var length = after - before;

            // read as many bytes as needed to read a total of 16 bytes, even if the scratch pad is not used
            reader.ReadBytes((int)(16 - length));
        }

        #endregion

        #region Properties

        public ulong LinkNameOffset { get; set; }
        public ulong HeaderAddress { get; set; }
        public CacheType CacheType { get; set; }
        public ScratchPad? ScratchPad { get; set; }

        #endregion
    }
}
