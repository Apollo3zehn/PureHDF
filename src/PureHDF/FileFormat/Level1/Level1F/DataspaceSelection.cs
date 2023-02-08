namespace PureHDF
{
    internal class DataspaceSelection
    {
        #region Constructors

        public DataspaceSelection(H5BaseReader reader)
        {
            Type = (SelectionType)reader.ReadUInt32();

            Info = Type switch
            {
                SelectionType.H5S_SEL_NONE => new H5S_SEL_NONE(reader),
                SelectionType.H5S_SEL_POINTS => new H5S_SEL_POINTS(reader),
                SelectionType.H5S_SEL_HYPER => new H5S_SEL_HYPER(reader),
                SelectionType.H5S_SEL_ALL => new H5S_SEL_ALL(reader),
                SelectionType.H5S_SEL_POINTS_SPECIAL_HANDLING => SpecialHandling(reader),
                _ => throw new NotSupportedException($"The dataspace selection type '{Type}' is not supported.")
            };
        }

        private static H5S_SEL SpecialHandling(H5BaseReader reader)
        {
            // jump position
            var jumpPosition = reader.ReadUInt32();
            var points = new H5S_SEL_POINTS(reader);

            reader.Seek(jumpPosition, SeekOrigin.Begin);

            return points;
        }

        #endregion

        #region Properties

        public SelectionType Type { get; set; }
        public H5S_SEL Info { get; set; }

        #endregion
    }
}
