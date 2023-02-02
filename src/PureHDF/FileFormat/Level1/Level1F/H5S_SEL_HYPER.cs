namespace PureHDF
{
    internal class H5S_SEL_HYPER : H5S_SEL
    {
        #region Fields

        private uint _version;

        #endregion

        #region Constructors

        public H5S_SEL_HYPER(H5BaseReader reader)
        {
            // version
            Version = reader.ReadUInt32();

            // SelectionInfo
            SelectionInfo = HyperslabSelectionInfo.Create(reader, Version);
        }

        #endregion

        #region Properties

        public uint Version
        {
            get
            {
                return _version;
            }
            set
            {
                if (!(1 <= value && value <= 2))
                    throw new FormatException($"Only version 1 and version 2 instances of type {nameof(H5S_SEL_HYPER)} are supported.");

                _version = value;
            }
        }

        public HyperslabSelectionInfo SelectionInfo { get; set; }

        #endregion
    }
}
