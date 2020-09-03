using System;
using System.IO;
using System.Text;

namespace HDF5.NET
{
    public class FractalHeapDirectBlock : FileBlock
    {
        #region Fields

        private Superblock _superblock;
        private byte _version;

        #endregion

        #region Constructors

        public FractalHeapDirectBlock(FractalHeapHeader header, BinaryReader reader, Superblock superblock) : base(reader)
        {
            _superblock = superblock;
            var headerSize = 0UL;

            // signature
            var signature = reader.ReadBytes(4);
            headerSize += 4;
            H5Utils.ValidateSignature(signature, FractalHeapDirectBlock.Signature);

            // version
            this.Version = reader.ReadByte();
            headerSize += 1;

            // heap header address
            this.HeapHeaderAddress = superblock.ReadOffset(reader);
            headerSize += superblock.OffsetsSize;

            // block offset
            var blockOffsetFieldSize = (int)Math.Ceiling(header.MaximumHeapSize / 8.0);
            this.BlockOffset = H5Utils.ReadUlong(this.Reader, (ulong)blockOffsetFieldSize);
            headerSize += (ulong)blockOffsetFieldSize;

            // checksum
            if (header.Flags.HasFlag(FractalHeapHeaderFlags.DirectBlocksAreChecksummed))
            {
                this.Checksum = reader.ReadUInt32();
                headerSize += 4;
            }

            this.HeaderSize = headerSize;
        }

        #endregion

        #region Properties

        public static byte[] Signature { get; } = Encoding.ASCII.GetBytes("FHDB");

        public byte Version
        {
            get
            {
                return _version;
            }
            set
            {
                if (value != 0)
                    throw new FormatException($"Only version 0 instances of type {nameof(FractalHeapDirectBlock)} are supported.");

                _version = value;
            }
        }

        public ulong HeapHeaderAddress { get; set; }
        public ulong BlockOffset { get; set; }
        public uint Checksum { get; set; }
        public byte[] ObjectData { get; set; }

        public FractalHeapHeader HeapHeader
        {
            get
            {
                this.Reader.BaseStream.Seek((long)this.HeapHeaderAddress, SeekOrigin.Begin);
                return new FractalHeapHeader(this.Reader, _superblock);
            }
        }

        public ulong HeaderSize { get; }

        #endregion
    }
}
