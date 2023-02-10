namespace PureHDF
{
    internal class DriverInfoBlock
    {
        #region Fields

        private byte _version;

        #endregion

        #region Constructors

        public DriverInfoBlock(H5BaseReader reader)
        {
            // version
            Version = reader.ReadByte();

            // reserved
            reader.ReadBytes(3);

            // driver info size
            DriverInfoSize = reader.ReadUInt32();

            // driver id
            DriverId = ReadUtils.ReadFixedLengthString(reader, 8);

            // driver info
            DriverInfo = DriverId switch
            {
                "NCSAmulti" => new MultiDriverInfo(reader),
                "NCSAfami" => new FamilyDriverInfo(reader),
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
                    throw new FormatException($"Only version 0 instances of type {nameof(DriverInfoBlock)} are supported.");

                _version = value;
            }
        }

        public uint DriverInfoSize { get; set; }
        public string DriverId { get; set; }
        public DriverInfo DriverInfo { get; set; }

        #endregion
    }
}
