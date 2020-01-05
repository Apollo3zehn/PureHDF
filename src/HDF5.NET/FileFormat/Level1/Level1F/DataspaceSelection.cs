namespace HDF5.NET
{
    public class DataspaceSelection
    {
        #region Constructors

        public DataspaceSelection()
        {
            //
        }

        #endregion

        #region Properties

        public SelectionType CollectionAddress { get; set; }
        public H5S_SEL SelectionInfo { get; set; }

        #endregion
    }
}
