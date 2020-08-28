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

        public FractalHeapIndirectBlock(FractalHeapHeader header, BinaryReader reader, Superblock superblock, uint rowCount) : base(reader)
        {
            _superblock = superblock;
            this.RowCount = rowCount;

            // signature
            var signature = reader.ReadBytes(4);
            H5Utils.ValidateSignature(signature, FractalHeapIndirectBlock.Signature);

            // version
            this.Version = reader.ReadByte();

            // heap header address
            this.HeapHeaderAddress = superblock.ReadOffset();

            // block offset
            var blockOffsetFieldSize = (int)Math.Ceiling(header.MaximumHeapSize / 8.0);
            this.BlockOffset = H5Utils.ReadUlong(this.Reader, (ulong)blockOffsetFieldSize);

            // direct and indirect block info
            var K = Math.Min(header.RootIndirectBlockRowsCount, header.MaxDirectRows) * header.TableWidth;
            var N = 0UL;

            if (header.RootIndirectBlockRowsCount > header.MaxDirectRows)
            {
                N = K - (header.MaxDirectRows * header.TableWidth);
            }

            this.DirectBlockInfos = new FractalHeapDirectBlockInfo[K];

            for (ulong i = 0; i < K; i++)
            {
                this.DirectBlockInfos[i].Address = _superblock.ReadOffset();

                if (header.IOFilterEncodedLength > 0)
                {
                    this.DirectBlockInfos[i].FilteredSize = _superblock.ReadLength();
                    this.DirectBlockInfos[i].FilterMask = reader.ReadUInt32();
                }
            }

            this.IndirectBlockAddresses = new ulong[N];

            for (ulong i = 0; i < N; i++)
            {
                this.IndirectBlockAddresses[i] = _superblock.ReadOffset();
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

        public FractalHeapDirectBlockInfo[] DirectBlockInfos { get; private set; }
        public ulong[] IndirectBlockAddresses { get; private set; }

        public uint RowCount { get; }

        #endregion
    }
}
