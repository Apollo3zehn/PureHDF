using System;
using System.IO;

namespace HDF5.NET
{
    public class DriverInfoBlock : FileBlock
    {
        #region Constructors

        public DriverInfoBlock(BinaryReader reader) : base(reader)
        {
            // version
            this.Version = reader.ReadByte(); 

            // reserved
            reader.ReadBytes(3);

            // driver info size
            this.DriverInfoSize = reader.ReadUInt32();

            // driver id
            this.DriverId = H5Utils.ReadFixedLengthString(reader, 8);

            // driver info
            this.DriverInfo = this.DriverId switch
            {
                "NCSAmulti" => new MultiDriverInfo(reader),
                "NCSAfami" => new FamilyDriverInfo(reader),
                _ => throw new NotSupportedException($"The driver ID '{this.DriverId}' is not supported.")
            };
        }

        #endregion

        #region Properties

        public byte Version { get; set; }
        public uint DriverInfoSize { get; set; }
        public string DriverId { get; set; }
        public DriverInfo DriverInfo { get; set; }

        #endregion
    }
}
