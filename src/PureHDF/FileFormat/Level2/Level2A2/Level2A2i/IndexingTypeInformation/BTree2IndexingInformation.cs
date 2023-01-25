namespace PureHDF
{
    internal class BTree2IndexingInformation : IndexingInformation
    {
        #region Constructors

        public BTree2IndexingInformation(H5BaseReader reader)
        {
            // node size
            NodeSize = reader.ReadUInt32();

            // split percent
            SplitPercent = reader.ReadByte();

            // merge percent
            MergePercent = reader.ReadByte();
        }

        #endregion

        #region Properties

        public uint NodeSize { get; set; }
        public byte SplitPercent { get; set; }
        public byte MergePercent { get; set; }

        #endregion
    }
}
