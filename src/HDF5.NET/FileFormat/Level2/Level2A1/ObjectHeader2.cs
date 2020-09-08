using System;
using System.Text;

namespace HDF5.NET
{
    public class ObjectHeader2 : ObjectHeader
    {
        #region Fields

        private byte _version;

        #endregion

        #region Constructors

        public ObjectHeader2(H5BinaryReader reader, Superblock superblock, byte version) : base(reader)
        {
            // version
            this.Version = version;

            // flags
            this.Flags = (ObjectHeaderFlags)reader.ReadByte();

            // access time, modification time, change time and birth time
            if (this.Flags.HasFlag(ObjectHeaderFlags.StoreFileAccessTimes))
            {
                this.AccessTime = reader.ReadUInt32();
                this.ModificationTime = reader.ReadUInt32();
                this.ChangeTime = reader.ReadUInt32();
                this.BirthTime = reader.ReadUInt32();
            }

            // maximum compact attributes count and minimum dense attributes count
            if (this.Flags.HasFlag(ObjectHeaderFlags.StoreNonDefaultAttributePhaseChangeValues))
            {
                this.MaximumCompactAttributesCount = reader.ReadUInt16();
                this.MinimumDenseAttributesCount = reader.ReadUInt16();
            }

            // size of chunk 0
            var chunkFieldSize = (byte)(1 << ((byte)this.Flags & 0x03));
            this.SizeOfChunk0 = H5Utils.ReadUlong(this.Reader, chunkFieldSize);

            // header messages
            var withCreationOrder = this.Flags.HasFlag(ObjectHeaderFlags.TrackAttributeCreationOrder);
            var messages = this.ReadHeaderMessages(reader, superblock, this.SizeOfChunk0, version: 2, withCreationOrder);
            this.HeaderMessages.AddRange(messages);

#warning H5OCache.c (L. 1595)  /* Gaps should only occur in chunks with no null messages */
#warning read gap and checksum
        }

        #endregion

        #region Properties

        public static byte[] Signature { get; } = Encoding.ASCII.GetBytes("OHDR");

        public byte Version
        {
            get
            {
                return _version;
            }
            set
            {
                if (value != 2)
                    throw new FormatException($"Only version 2 instances of type {nameof(ObjectHeader2)} are supported.");

                _version = value;
            }
        }

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
