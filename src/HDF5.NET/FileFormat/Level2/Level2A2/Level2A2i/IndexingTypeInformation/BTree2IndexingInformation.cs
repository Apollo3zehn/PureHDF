namespace HDF5.NET
{
    public class BTree2IndexingInformation : IndexingInformation
    {
        #region Constructors

        public BTree2IndexingInformation(H5BinaryReader reader) : base(reader)
        {
            // node size
            this.NodeSize = reader.ReadUInt32();

            // split percent
            this.SplitPercent = reader.ReadByte();

            // merge percent
            this.MergePercent = reader.ReadByte();
        }

        #endregion

        #region Properties

        public uint NodeSize { get; set; }
        public byte SplitPercent { get; set; }
        public byte MergePercent { get; set; }

        #endregion
    }
}
