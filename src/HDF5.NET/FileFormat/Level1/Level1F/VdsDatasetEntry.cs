namespace HDF5.NET
{
    public class VdsDatasetEntry : FileBlock
    {
        #region Constructors

        public VdsDatasetEntry(H5BinaryReader reader) : base(reader)
        {
#warning Is reading null terminated string correct?
            // source file name
            this.SourceFileName = H5Utils.ReadNullTerminatedString(reader, pad: true);

            // source dataset
            this.SourceDataset = H5Utils.ReadNullTerminatedString(reader, pad: true);

            // source selection
            this.SourceSelection = new DataspaceSelection(reader);

            // virtual selection
            this.VirtualSelection = new DataspaceSelection(reader);
        }

        #endregion

        #region Properties

        public string SourceFileName { get; set; }
        public string SourceDataset { get; set; }
        public DataspaceSelection SourceSelection { get; set; }
        public DataspaceSelection VirtualSelection { get; set; }

        #endregion
    }
}
