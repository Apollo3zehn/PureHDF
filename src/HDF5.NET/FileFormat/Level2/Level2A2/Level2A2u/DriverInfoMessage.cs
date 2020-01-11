using System;
using System.IO;

namespace HDF5.NET
{
    public class DriverInfoMessage : Message
    {
        #region Fields

        private byte _version;

        #endregion

        #region Constructors

        public DriverInfoMessage(BinaryReader reader) : base(reader)
        {
            // version
            this.Version = reader.ReadByte();

            // driver id
            this.DriverId = H5Utils.ReadFixedLengthString(reader, 8);

            // driver info size
            this.DriverInfoSize = reader.ReadUInt16();

            // driver info
            this.DriverInfo = this.DriverId switch
            {
                "NCSAmulti" => new MultiDriverInfo(reader),
                "NCSAfami"  => new FamilyDriverInfo(reader),
                _           => throw new NotSupportedException($"The driver ID '{this.DriverId}' is not supported.")
            };
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
                if (value != 0)
                    throw new FormatException($"Only version 0 instances of type {nameof(DriverInfoMessage)} are supported.");

                _version = value;
            }
        }

        public string DriverId { get; set; }
        public ushort DriverInfoSize { get; set; }
        public DriverInfo DriverInfo { get; set; }

        #endregion
    }
}
