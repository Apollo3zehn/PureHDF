﻿namespace PureHDF
{
    internal class Superblock23 : Superblock
    {
        #region Constructors

        public Superblock23(H5BaseReader reader, byte version)
        {
            SuperBlockVersion = version;
            OffsetsSize = reader.ReadByte();
            LengthsSize = reader.ReadByte();
            FileConsistencyFlags = (FileConsistencyFlags)reader.ReadByte();
            BaseAddress = ReadOffset(reader);
            SuperblockExtensionAddress = ReadOffset(reader);
            EndOfFileAddress = ReadOffset(reader);
            RootGroupObjectHeaderAddress = ReadOffset(reader);
            SuperblockChecksum = reader.ReadUInt32();
        }

        #endregion

        #region Properties

        public ulong SuperblockExtensionAddress { get; set; }
        public ulong RootGroupObjectHeaderAddress { get; set; }
        public uint SuperblockChecksum { get; set; }

        #endregion
    }
}
