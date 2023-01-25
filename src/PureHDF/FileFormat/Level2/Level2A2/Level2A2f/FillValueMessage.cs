﻿namespace PureHDF
{
    // https://support.hdfgroup.org/HDF5/doc_resource/H5Fill_Behavior.html
    internal class FillValueMessage : Message
    {
        #region Fields

        private static readonly byte[] _defaultValue = new byte[] { 0 };

        private byte[]? _value;
        private byte _version;

        #endregion

        #region Constructors

        public FillValueMessage(H5BaseReader reader)
        {
            // see also H5dcpl.c (H5P_is_fill_value_defined) and H5Dint.c (H5D__update_oh_info):
            // if size = 0 then default value should be applied
            // if size = -1 then fill value is explicitly undefined

            // version
            Version = reader.ReadByte();

            uint size;

            switch (Version)
            {
                case 1:

                    AllocationTime = (SpaceAllocationTime)reader.ReadByte();
                    FillTime = (FillValueWriteTime)reader.ReadByte();

                    var isDefined1 = reader.ReadByte() == 1;

                    if (isDefined1)
                    {
                        size = reader.ReadUInt32();
                        Value = reader.ReadBytes((int)size);
                    }

                    break;

                case 2:

                    AllocationTime = (SpaceAllocationTime)reader.ReadByte();
                    FillTime = (FillValueWriteTime)reader.ReadByte();
                    var isDefined2 = reader.ReadByte() == 1;

                    if (isDefined2)
                    {
                        size = reader.ReadUInt32();
                        Value = reader.ReadBytes((int)size);
                    }

                    break;

                case 3:

                    var flags = reader.ReadByte();
                    AllocationTime = (SpaceAllocationTime)((flags & 0x03) >> 0);   // take only bits 0 and 1
                    FillTime = (FillValueWriteTime)((flags & 0x0C) >> 2);          // take only bits 2 and 3
                    var isUndefined = (flags & (1 << 4)) > 0;                           // take only bit 4
                    var isDefined3 = (flags & (1 << 5)) > 0;                            // take only bit 5

                    // undefined
                    if (isUndefined)
                    {
                        Value = null;
                    }
                    // defined
                    else if (isDefined3)
                    {
                        size = reader.ReadUInt32();
                        Value = reader.ReadBytes((int)size);
                    }
                    // default
                    else
                    {
                        Value = Array.Empty<byte>();
                    }

                    break;

                default:
                    break;
            }
        }

        // Create a default fill value message (required for old fill value messages which are optional)
        public FillValueMessage(SpaceAllocationTime allocationTime)
        {
            Version = 3;
            AllocationTime = allocationTime;
            FillTime = FillValueWriteTime.IfSetByUser;
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
                if (!(1 <= value && value <= 3))
                    throw new FormatException($"Only version 1-3 instances of type {nameof(FillValueMessage)} are supported.");

                _version = value;
            }
        }

        public SpaceAllocationTime AllocationTime { get; set; }

        public FillValueWriteTime FillTime { get; set; }

        public byte[]? Value
        {
            set
            {
                _value = value;
            }
            get
            {
                if (_value?.Length == 0)
                    return _defaultValue;

                else
                    return _value;
            }
        }

        #endregion
    }
}
