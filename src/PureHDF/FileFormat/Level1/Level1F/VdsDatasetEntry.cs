﻿namespace PureHDF
{
    internal class VdsDatasetEntry
    {
        #region Constructors

        public VdsDatasetEntry(H5BaseReader reader)
        {
            // source file name
            SourceFileName = H5ReadUtils.ReadNullTerminatedString(reader, pad: false);

            // source dataset
            SourceDataset = H5ReadUtils.ReadNullTerminatedString(reader, pad: false);

            // source selection
            SourceSelection = new DataspaceSelection(reader);

            // virtual selection
            VirtualSelection = new DataspaceSelection(reader);
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
