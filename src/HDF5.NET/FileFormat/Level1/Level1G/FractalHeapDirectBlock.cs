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
            var headerSize = 0;

            // signature
            var signature = reader.ReadBytes(4);
            headerSize += 4;
            H5Utils.ValidateSignature(signature, FractalHeapDirectBlock.Signature);

            // version
            this.Version = reader.ReadByte();
            headerSize += 1;

            // heap header address
            this.HeapHeaderAddress = superblock.ReadOffset();
            headerSize += superblock.OffsetsSize;

            // block offset
            var blockOffsetFieldSize = (int)Math.Ceiling(header.MaximumHeapSize / 8.0);
            this.BlockOffset = this.ReadUlong((ulong)blockOffsetFieldSize);
            headerSize += blockOffsetFieldSize;

            // checksum
            if (header.Flags.HasFlag(FractalHeapHeaderFlags.DirectBlocksAreChecksummed))
            {
                this.Checksum = reader.ReadUInt32();
                headerSize += 4;
            }

            // object data
#warning Check this.
            var row = 0UL;

            if (this.BlockOffset > header.StartingBlockSize * header.TableWidth)
                row = (ulong)Math.Log(this.BlockOffset, 2) - header.StartingBits + 1;

            var size = (int)header.RowBlockSizes[row];
            size -= headerSize;

            //this.ObjectData = reader.ReadBytes(headerSize);

            // alternative:
            //herr_t
            //H5HF_dtable_lookup(const H5HF_dtable_t *dtable, hsize_t off, unsigned *row, unsigned *col)
            //{
            //    /* Check for offset in first row */
            //    if(off < dtable->num_id_first_row) {
            //        *row = 0;
            //        H5_CHECKED_ASSIGN(*col, unsigned, (off / dtable->cparam.start_block_size), hsize_t);
            //    } /* end if */
            //    else {
            //        unsigned high_bit = H5VM_log2_gen(off);  /* Determine the high bit in the offset */
            //        hsize_t off_mask = ((hsize_t)1) << high_bit;       /* Compute mask for determining column */

            //        *row = (high_bit - dtable->first_row_bits) + 1;
            //        H5_CHECKED_ASSIGN(*col, unsigned, ((off - off_mask) / dtable->row_block_size[*row]), hsize_t);
            //    } /* end else */
            //} /* end H5HF_dtable_lookup() */
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

        #endregion
    }
}
