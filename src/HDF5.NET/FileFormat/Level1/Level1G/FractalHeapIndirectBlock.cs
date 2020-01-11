using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace HDF5.NET
{
    public class FractalHeapIndirectBlock : FileBlock
    {
        #region Fields

#warning OK like this?
        private Superblock _superblock;
        private byte _version;

        #endregion

        #region Constructors

        public FractalHeapIndirectBlock(BinaryReader reader, Superblock superblock) : base(reader)
        {
            _superblock = superblock;

            // signature
            var signature = reader.ReadBytes(4);
            H5Utils.ValidateSignature(signature, FractalHeapIndirectBlock.Signature);

            // version
            this.Version = reader.ReadByte();

            // heap header address
            this.HeapHeaderAddress = superblock.ReadOffset();
            var header = this.HeapHeader;

            // block offset
#warning correct?
            var blockOffsetFieldSize = (header.MaximumHeapSize + 1) / 8;
            this.BlockOffset = this.ReadUlong((ulong)blockOffsetFieldSize);

#warning read missing data

            // checksum
            this.Checksum = reader.ReadUInt32();
        }

        #endregion

        #region Properties

        public static byte[] Signature { get; } = Encoding.ASCII.GetBytes("FHIB");

        public byte Version
        {
            get
            {
                return _version;
            }
            set
            {
                if (value != 0)
                    throw new FormatException($"Only version 0 instances of type {nameof(FractalHeapIndirectBlock)} are supported.");

                _version = value;
            }
        }

        public ulong HeapHeaderAddress { get; set; }
        public ulong BlockOffset { get; set; }
        public List<ulong> ChildDirectBlockAdresses { get; set; }
        public List<ulong> FilteredDirectBlockSizes { get; set; }
        public List<ulong> DirectBlockFilterMask { get; set; }
        public uint Checksum { get; set; }

        public FractalHeapHeader HeapHeader
        {
            get
            {
                this.Reader.BaseStream.Seek((long)this.HeapHeaderAddress, SeekOrigin.Begin);
                return new FractalHeapHeader(this.Reader, _superblock);
            }
        }

        #endregion
    }
}
