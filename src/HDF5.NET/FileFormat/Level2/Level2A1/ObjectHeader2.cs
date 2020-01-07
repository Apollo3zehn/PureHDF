using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace HDF5.NET
{
    public class ObjectHeader2 : ObjectHeader
    {
        #region Constructors

        public ObjectHeader2(byte version, BinaryReader reader) : base(reader)
        {
            this.Version = version;
            this.Flags = (ObjectHeaderFlags)reader.ReadByte();

            if (this.Flags.HasFlag(ObjectHeaderFlags.StoreFileAccessTimes))
            {
                this.AccessTime = reader.ReadUInt32();
                this.ModificationTime = reader.ReadUInt32();
                this.ChangeTime = reader.ReadUInt32();
                this.BirthTime = reader.ReadUInt32();
            }

            if (this.Flags.HasFlag(ObjectHeaderFlags.StoreNonDefaultAttributePhaseChangeValues))
            {
                this.MaximumCompactAttributesCount = reader.ReadUInt16();
                this.MinimumDenseAttributesCount = reader.ReadUInt16();
            }

            var chunkFieldSize = (byte)(1 << ((byte)this.Flags & 0xFC));
            this.SizeOfChunk0 = this.ReadUlong(chunkFieldSize);

            // read header messages
            this.HeaderMessages = new List<HeaderMessage>();

#warning read messages
            //while (remainingBytes > 0)
            //{
            //    var message = new HeaderMessage(reader, superblock, 1);
            //    this.HeaderMessages.Add(message);
            //}

#warning read gap and checksum
        }

        #endregion

        #region Properties

        public static byte[] Signature { get; } = Encoding.ASCII.GetBytes("OHDR");

        public byte Version { get; set; }
        public ObjectHeaderFlags Flags { get; set; }
        public uint AccessTime { get; set; }
        public uint ModificationTime { get; set; }
        public uint ChangeTime { get; set; }
        public uint BirthTime { get; set; }
        public ushort MaximumCompactAttributesCount { get; set; }
        public ushort MinimumDenseAttributesCount { get; set; }
        public ulong SizeOfChunk0 { get; set; }
        public uint Checksum { get; set; }

        #endregion
    }
}
