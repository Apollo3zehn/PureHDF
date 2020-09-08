using System;

namespace HDF5.NET
{
    public class H5S_SEL_HYPER : H5S_SEL
    {
        #region Fields

        private uint _version;

        #endregion

        #region Constructors

        public H5S_SEL_HYPER(H5BinaryReader reader) : base(reader)
        {
            // version
            this.Version = reader.ReadUInt32();

            // hyperslab selection info
            this.HyperslabSelectionInfo = this.Version switch
            {
                1 => new HyperslabSelectionInfo1(reader),
                2 => new HyperslabSelectionInfo2(reader),
                _ => throw new NotSupportedException($"Only {nameof(H5S_SEL_HYPER)} of version 1 or 2 are supported.")
            };
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

        public HyperslabSelectionInfo HyperslabSelectionInfo { get; set; }

        #endregion
    }
}
