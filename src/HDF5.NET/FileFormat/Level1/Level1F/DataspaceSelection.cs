namespace HDF5.NET
{
    internal class DataspaceSelection : FileBlock
    {
        #region Constructors

        public DataspaceSelection(H5BinaryReader reader) : base(reader)
        {
            SelectionType = (SelectionType)reader.ReadUInt32();

            SelectionInfo = SelectionType switch
            {
                SelectionType.H5S_SEL_NONE      => new H5S_SEL_NONE(reader),
                SelectionType.H5S_SEL_POINTS    => new H5S_SEL_POINTS(reader),
                SelectionType.H5S_SEL_HYPER     => new H5S_SEL_HYPER(reader),
                SelectionType.H5S_SEL_ALL       => new H5S_SEL_ALL(reader),
                _ => throw new NotSupportedException($"The dataspace selection type '{SelectionType}' is not supported.")
            };
        }

        #endregion

        #region Properties

        public SelectionType SelectionType { get; set; }
        public H5S_SEL SelectionInfo { get; set; }

        #endregion
    }
}
