namespace HDF5.NET
{
    public struct FixedArrayDataBlockElement
    {
        #region Properties

        public ulong Address { get; set; }

        public uint ChunkSize { get; set; }

        public uint FilterMask { get; set; }

        #endregion
    }
}
