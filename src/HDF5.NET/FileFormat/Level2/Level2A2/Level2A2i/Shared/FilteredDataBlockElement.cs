namespace HDF5.NET
{
    public class FilteredDataBlockElement : DataBlockElement
    {
        #region Properties

        public uint ChunkSize { get; set; }

        public uint FilterMask { get; set; }

        #endregion
    }
}
