namespace HDF5.NET
{
    public class H5S_SEL_HYPER : H5S_SEL
    {
        #region Constructors

        public H5S_SEL_HYPER()
        {
            //
        }

        #endregion

        #region Properties

        public uint Version { get; set; }
        public HyperslabSelectionInfo HyperslabSelectionInfo { get; set; }

        #endregion
    }
}
