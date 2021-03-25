using System;
using System.IO;
using System.Text;

namespace HDF5.NET
{
    internal class LocalHeap : FileBlock
    {
        #region Fields

        private byte _version;
        private byte[] _data;

        #endregion

        #region Constructors

        public LocalHeap(H5BinaryReader reader, Superblock superblock) : base(reader)
        {
            // signature
            var signature = reader.ReadBytes(4);
            H5Utils.ValidateSignature(signature, LocalHeap.Signature);

            // version
            this.Version = reader.ReadByte();

            // reserved
            reader.ReadBytes(3);

            // data segment size
            this.DataSegmentSize = superblock.ReadLength(reader);

            // free list head offset
            this.FreeListHeadOffset = superblock.ReadLength(reader);

            // data segment address
            this.DataSegmentAddress = superblock.ReadOffset(reader);
        }

        #endregion

        #region Properties

        public static byte[] Signature { get; set; } = Encoding.ASCII.GetBytes("HEAP");

        public byte Version
        {
            get
            {
                return _version;
            }
            set
            {
                if (value != 0)
                    throw new FormatException($"Only version 0 instances of type {nameof(LocalHeap)} are supported.");

                _version = value;
            }
        }

        public ulong DataSegmentSize { get; set; }
        public ulong FreeListHeadOffset { get; set; }
        public ulong DataSegmentAddress { get; set; }

        public byte[] Data
        {
            get
            {
                if (_data is null)
                {
                    this.Reader.Seek((long)this.DataSegmentAddress, SeekOrigin.Begin);
                    _data = this.Reader.ReadBytes((int)this.DataSegmentSize);
                }

                return _data;
            }
        }

        #endregion

        #region Methods

        public string GetObjectName(ulong offset)
        {
            var end = Array.IndexOf(this.Data, (byte)0, (int)offset);
            var bytes = this.Data[(int)offset..end];

            return Encoding.ASCII.GetString(bytes);           
        }

        #endregion
    }
}
