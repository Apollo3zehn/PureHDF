namespace HDF5.NET
{
    internal class DriverInfoMessage : Message
    {
        #region Fields

        private byte _version;

        #endregion

        #region Constructors

        public DriverInfoMessage(H5BinaryReader reader)
        {
            // version
            Version = reader.ReadByte();

            // driver id
            DriverId = H5ReadUtils.ReadFixedLengthString(reader, 8);

            // driver info size
            DriverInfoSize = reader.ReadUInt16();

            // driver info
            DriverInfo = DriverId switch
            {
                "NCSAmulti" => new MultiDriverInfo(reader),
                "NCSAfami"  => new FamilyDriverInfo(reader),
                _ => throw new NotSupportedException($"The driver ID '{DriverId}' is not supported.")
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
