using PureHDF.VFD;

namespace PureHDF
{
    internal class DataspaceSelection
    {
        #region Constructors

        public DataspaceSelection(H5DriverBase driver)
        {
            Type = (SelectionType)driver.ReadUInt32();

            Info = Type switch
            {
                SelectionType.H5S_SEL_NONE => new H5S_SEL_NONE(driver),
                SelectionType.H5S_SEL_POINTS => new H5S_SEL_POINTS(driver),
                SelectionType.H5S_SEL_HYPER => new H5S_SEL_HYPER(driver),
                SelectionType.H5S_SEL_ALL => new H5S_SEL_ALL(driver),
                SelectionType.H5S_SEL_POINTS_SPECIAL_HANDLING => SpecialHandling(driver),
                _ => throw new NotSupportedException($"The dataspace selection type '{Type}' is not supported.")
            };
        }

        private static H5S_SEL SpecialHandling(H5DriverBase driver)
        {
            // jump position
            var jumpPosition = driver.ReadUInt32();
            var points = new H5S_SEL_POINTS(driver);

            driver.Seek(jumpPosition, SeekOrigin.Begin);

            return points;
        }

        #endregion

        #region Properties

        public SelectionType Type { get; set; }
        public H5S_SEL Info { get; set; }

        #endregion
    }
}
