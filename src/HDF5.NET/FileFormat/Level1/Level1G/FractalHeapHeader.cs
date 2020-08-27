using System;
using System.IO;
using System.Text;

namespace HDF5.NET
{
    public class FractalHeapHeader : FileBlock
    {
        #region Fields

        private byte _version;

        #endregion

        #region Constructors

        public FractalHeapHeader(BinaryReader reader, Superblock superblock) : base(reader)
        {
            // signature
            var signature = reader.ReadBytes(4);
            H5Utils.ValidateSignature(signature, FractalHeapHeader.Signature);

            // version
            this.Version = reader.ReadByte();

            // heap ID length
            this.HeapIdLength = reader.ReadUInt16();

            // I/O filter encoder length
            this.IOFilterEncodedLength = reader.ReadUInt16();

            // flags
            this.Flags = (FractalHeapHeaderFlags)reader.ReadByte();

            /* next group */

            // managed objects maximum size
            this.ManagedObjectsMaximumSize = reader.ReadUInt32();

            // next huge object id
            this.NextHugeObjectId = superblock.ReadLength();

            // huge objects BTree2 address
            this.HugeObjectsBTree2Address = superblock.ReadOffset();

            // managed blocks free space amount
            this.ManagedBlocksFreeSpaceAmount = superblock.ReadLength();

            // managed block free space manager address
            this.ManagedBlockFreeSpaceManagerAddress = superblock.ReadOffset();

            // heap managed space amount
            this.HeapManagedSpaceAmount = superblock.ReadLength();

            // heap allocated managed space amount
            this.HeapAllocatedManagedSpaceAmount = superblock.ReadLength();

            // managed space direct block allocation iterator offset
            this.ManagedSpaceDirectBlockAllocationIteratorOffset = superblock.ReadLength();

            // heap managed objects count
            this.HeapManagedObjectsCount = superblock.ReadLength();

            // heap huge objects size
            this.HeapHugeObjectsSize = superblock.ReadLength();

            // heap huge objects cound
            this.HeapHugeObjectsCount = superblock.ReadLength();

            // heap tiny objects size
            this.HeapTinyObjectsSize = superblock.ReadLength();

            // heap tiny objects count
            this.HeapTinyObjectsCount = superblock.ReadLength();

            /* next group */

            // table width
            this.TableWidth = reader.ReadUInt16();

            // starting block size
            this.StartingBlockSize = superblock.ReadLength();

            // maximum direct block size
            this.MaximumDirectBlockSize = superblock.ReadLength();

            // maximum heap size
            this.MaximumHeapSize = reader.ReadUInt16();

            // root indirect block rows starting number
            this.RootIndirectBlockRowsStartingNumber = reader.ReadUInt16();

            // root block address
            this.RootBlockAddress = superblock.ReadOffset();

            // root indirect block rows count
            this.RootIndirectBlockRowsCount = reader.ReadUInt16();

            /* next group */

            // filtered root direct block size, I/O filter mask and I/O filter info
            if (this.IOFilterEncodedLength > 0)
            {
                this.FilteredRootDirectBlockSize = superblock.ReadLength();
                this.IOFilterMask = reader.ReadUInt32();
                this.IOFilterInfo = new FilterPipelineMessage(reader);
            }           

            // checksum
            this.Checksum = reader.ReadUInt32();

            // cache some values
            this.CalculateBlockSizeTables();
        }

        #endregion

        #region Properties

        public static byte[] Signature { get; } = Encoding.ASCII.GetBytes("FRHP");

        public byte Version
        {
            get
            {
                return _version;
            }
            set
            {
                if (value != 0)
                    throw new FormatException($"Only version 0 instances of type {nameof(FractalHeapHeader)} are supported.");

                _version = value;
            }
        }

        public ushort HeapIdLength { get; set; }
        public ushort IOFilterEncodedLength { get; set; }
        public FractalHeapHeaderFlags Flags { get; set; }
        public uint ManagedObjectsMaximumSize { get; set; }

        public ulong NextHugeObjectId { get; set; }
        public ulong HugeObjectsBTree2Address { get; set; }
        public ulong ManagedBlocksFreeSpaceAmount { get; set; }
        public ulong ManagedBlockFreeSpaceManagerAddress { get; set; }
        public ulong HeapManagedSpaceAmount { get; set; }
        public ulong HeapAllocatedManagedSpaceAmount { get; set; }
        public ulong ManagedSpaceDirectBlockAllocationIteratorOffset { get; set; }
        public ulong HeapManagedObjectsCount { get; set; }

        public ulong HeapHugeObjectsSize { get; set; }
        public ulong HeapHugeObjectsCount { get; set; }

        public ulong HeapTinyObjectsSize { get; set; }
        public ulong HeapTinyObjectsCount { get; set; }

        public ushort TableWidth { get; set; }
        public ulong StartingBlockSize { get; set; }
        public ulong MaximumDirectBlockSize { get; set; }
        public ushort MaximumHeapSize { get; set; }
        public ushort RootIndirectBlockRowsStartingNumber { get; set; }
        public ulong RootBlockAddress { get; set; }
        public ushort RootIndirectBlockRowsCount { get; set; }

        public ulong FilteredRootDirectBlockSize { get; set; }
        public uint IOFilterMask { get; set; }
        public FilterPipelineMessage? IOFilterInfo { get; set; }
        public uint Checksum { get; set; }

        public ulong[] RowBlockSizes { get; private set; }
        public ulong[] RowBlockOffsets { get; private set; }

        public ulong MaxDirectRows { get; private set; }
        public ulong StartingBits { get; private set; }

        #endregion

        #region Methods

        private void CalculateBlockSizeTables()
        {
            // from H5HFdtable.c
            this.StartingBits = (ulong)Math.Log(this.StartingBlockSize, 2);
            var firstRowBits = (ulong)(this.StartingBits + Math.Log(this.TableWidth, 2));

            var maxDirectBits = (ulong)Math.Log(this.MaximumDirectBlockSize, 2);
            this.MaxDirectRows = maxDirectBits - this.StartingBits + 2;

            var maxRootRows = this.MaximumHeapSize - firstRowBits;

            this.RowBlockSizes = new ulong[maxRootRows];
            this.RowBlockOffsets = new ulong[maxRootRows];

            var tmpBlockSize = this.StartingBlockSize;
            var accumulatedBlockOffset = this.StartingBlockSize * this.TableWidth;

            this.RowBlockSizes[0] = tmpBlockSize;
            this.RowBlockOffsets[0] = 0;

            for (ulong i = 0; i < maxRootRows; i++)
            {
                this.RowBlockSizes[i] = tmpBlockSize;
                this.RowBlockOffsets[i] = accumulatedBlockOffset;
                tmpBlockSize *= 2;
                accumulatedBlockOffset *= 2;
            }
        }

        #endregion
    }
}
