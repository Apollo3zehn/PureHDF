using System;
using System.IO;

namespace HDF5.NET
{
    public class DataspaceSelection : FileBlock
    {
        #region Constructors

        public DataspaceSelection(BinaryReader reader) : base(reader)
        {
            this.SelectionType = (SelectionType)reader.ReadUInt32();

            this.SelectionInfo = this.SelectionType switch
            {
                SelectionType.H5S_SEL_NONE      => new H5S_SEL_NONE(reader),
                SelectionType.H5S_SEL_POINTS    => new H5S_SEL_POINTS(reader),
                SelectionType.H5S_SEL_HYPER     => new H5S_SEL_HYPER(reader),
                SelectionType.H5S_SEL_ALL       => new H5S_SEL_ALL(reader),
                _                               => throw new NotSupportedException($"The dataspace selection type '{this.SelectionType}' is not supported.")
            };
        }

        #endregion

        #region Properties

        public SelectionType SelectionType { get; set; }
        public H5S_SEL SelectionInfo { get; set; }

        #endregion
    }
}
