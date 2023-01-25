﻿namespace PureHDF
{
    internal class H5S_SEL_ALL : H5S_SEL
    {
        #region Fields

        private uint _version;

        #endregion

        #region Constructors

        public H5S_SEL_ALL(H5BaseReader reader)
        {
            // version
            Version = reader.ReadUInt32();

            // reserved
            reader.ReadBytes(8);
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
                    throw new FormatException($"Only version 1 instances of type {nameof(H5S_SEL_ALL)} are supported.");

                _version = value;
            }
        }

        #endregion
    }
}
