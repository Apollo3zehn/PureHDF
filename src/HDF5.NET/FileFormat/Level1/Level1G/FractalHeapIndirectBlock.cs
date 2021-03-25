using System;
using System.IO;
using System.Text;

namespace HDF5.NET
{
    internal class FractalHeapIndirectBlock : FileBlock
    {
        #region Fields

#warning OK like this?
        private Superblock _superblock;
        private byte _version;

        #endregion

        #region Constructors

        public FractalHeapIndirectBlock(FractalHeapHeader header, H5BinaryReader reader, Superblock superblock, uint rowCount) : base(reader)
        {
            _superblock = superblock;
            this.RowCount = rowCount;

            // signature
            var signature = reader.ReadBytes(4);
            H5Utils.ValidateSignature(signature, FractalHeapIndirectBlock.Signature);

            // version
            this.Version = reader.ReadByte();

            // heap header address
            this.HeapHeaderAddress = superblock.ReadOffset(reader);

            // block offset
            var blockOffsetFieldSize = (int)Math.Ceiling(header.MaximumHeapSize / 8.0);
            this.BlockOffset = H5Utils.ReadUlong(this.Reader, (ulong)blockOffsetFieldSize);

            // H5HFcache.c (H5HF__cache_iblock_deserialize)
            var length = rowCount * header.TableWidth;
            this.Entries = new FractalHeapEntry[length];

            for (uint i = 0; i < this.Entries.Length; i++)
            {
                /* Decode child block address */
                this.Entries[i].Address = _superblock.ReadOffset(reader);

                /* Check for heap with I/O filters */
                if (header.IOFilterEncodedLength > 0)
                {
                    /* Decode extra information for direct blocks */
                    if (i < (header.MaxDirectRows * header.TableWidth))
                    {
                        /* Size of filtered direct block */
                        this.Entries[i].FilteredSize = _superblock.ReadLength(reader);

                        /* I/O filter mask for filtered direct block */
                        this.Entries[i].FilterMask = reader.ReadUInt32();
                    }
                }


                /* Count child blocks */
                if (!superblock.IsUndefinedAddress(this.Entries[i].Address))
                {
                    this.ChildCount++;
                    this.MaxChildIndex = i;
                }
            }

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
        public uint Checksum { get; set; }

        public FractalHeapHeader HeapHeader
        {
            get
            {
                this.Reader.Seek((long)this.HeapHeaderAddress, SeekOrigin.Begin);
                return new FractalHeapHeader(this.Reader, _superblock);
            }
        }

        public FractalHeapEntry[] Entries { get; private set; }
        public uint RowCount { get; }
        public uint ChildCount { get; }
        public uint MaxChildIndex { get; }

        #endregion
    }
}
