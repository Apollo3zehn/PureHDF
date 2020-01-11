using System;
using System.Collections.Generic;
using System.IO;

namespace HDF5.NET
{
    public class FilterPipelineMessage : Message
    {
        #region Fields

        private byte _version;
        private byte _filterCount;

        #endregion

        #region Constructors

        public FilterPipelineMessage(BinaryReader reader) : base(reader)
        {
            // version
            this.Version = reader.ReadByte();

            // filter count
            this.FilterCount = reader.ReadByte();

            // filter descriptions
            this.FilterDescriptions = new List<FilterDescription>(this.FilterCount);

            for (int i = 0; i < this.FilterCount; i++)
            {
                this.FilterDescriptions.Add(new FilterDescription(reader, this.Version));
            }
        }

        #endregion

        #region Properties

        public byte Version
        {
            get
            {
                return _version;
            }
            set
            {
                if (!(1 <= value && value <= 2))
                    throw new FormatException($"Only version 1 and 2 instances of type {nameof(FilterPipelineMessage)} are supported.");

                _version = value;
            }
        }

        public byte FilterCount
        {
            get
            {
                return _filterCount;
            }
            set
            {
                if (value > 32)
                    throw new FormatException($"An instance of type {nameof(FilterPipelineMessage)} can only contain a maximum of 32 filters.");

                _filterCount = value;
            }
        }

        public List<FilterDescription> FilterDescriptions { get; set; }

        #endregion
    }
}
