namespace HDF5.NET
{
    internal struct FractalHeapEntry
    {
        #region Properties

        public ulong Address { get; set; }
        public ulong FilteredSize { get; set; }
        public uint FilterMask { get; set; }

        #endregion
    }
}
