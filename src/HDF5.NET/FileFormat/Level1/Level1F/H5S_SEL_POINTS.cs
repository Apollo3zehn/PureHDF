using System;

namespace HDF5.NET
{
    public class H5S_SEL_POINTS : H5S_SEL
    {
        #region Fields

        private uint _version;

        #endregion

        #region Constructors

        public H5S_SEL_POINTS(H5BinaryReader reader) : base(reader)
        {
            // version
            this.Version = reader.ReadUInt32();

            // reserved
            reader.ReadBytes(4);

            // length
            this.Length = reader.ReadUInt32();

            // rank
            this.Rank = reader.ReadUInt32();

            // point count
            this.PointCount = reader.ReadUInt32();

            // point data
            var totalSize = this.PointCount * this.Rank;
            this.PointData = new uint[totalSize];

            for (int i = 0; i < totalSize; i++)
            {
                this.PointData[i] = reader.ReadUInt32();
            }
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
                if (value != 1)
                    throw new FormatException($"Only version 1 instances of type {nameof(H5S_SEL_POINTS)} are supported.");

                _version = value;
            }
        }

        public uint Length { get; set; }
        public uint Rank { get; set; }
        public uint PointCount { get; set; }
        public uint[] PointData { get; set; }

        #endregion
    }
}
