namespace HDF5.NET
{
    public class FractalHeapHeader
    {
        #region Constructors

        public FractalHeapHeader()
        {
            //
        }

        #endregion

        #region Properties

        public byte[] Signature { get; set; }
        public byte Version { get; set; }
        public ushort HeapIdLength { get; set; }
        public ushort IOFilterEncodedLength { get; set; }
        public byte Flags { get; set; }

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
        public ushort RootIndirectBlockRowsCount { get; set; }
        public ulong SizeOfFilteredRootDirectBlock { get; set; }
        public uint IOFilterMask { get; set; }
        public byte[] IOFilterInformation { get; set; }
        public uint Checksum { get; set; }

        #endregion
    }
}
