using System.IO;

namespace HDF5.NET
{
    public class DriverInfoMessage : Message
    {
        #region Constructors

        public DriverInfoMessage(BinaryReader reader) : base(reader)
        {
            //
        }

        #endregion

        #region Properties

        public byte Version { get; set; }
        public string DriverId { get; set; }
        public ushort GroupInternalNodeK { get; set; }
        public ushort GroupLeafNodeK { get; set; }

        #endregion
    }
}
