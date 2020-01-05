namespace HDF5.NET
{
    public class BTree2IndexingInformation : IndexingInformation
    {
        #region Constructors

        public BTree2IndexingInformation()
        {
            //
        }

        #endregion

        #region Properties

        public uint NodeSize { get; set; }
        public byte SplitPercent { get; set; }
        public byte MergePercent { get; set; }

        #endregion
    }
}
