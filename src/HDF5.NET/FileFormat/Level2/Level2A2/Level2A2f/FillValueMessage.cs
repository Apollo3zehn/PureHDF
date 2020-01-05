using System.IO;

namespace HDF5.NET
{
    public class FillValueMessage : Message
    {
#warning Remember to parse correctly depending on the version

        #region Constructors

        public FillValueMessage(BinaryReader reader) : base(reader)
        {
            //
        }

        #endregion

        #region Properties

        public byte Version { get; set; }
        public SpaceAllocationTime SpaceAllocationTime { get; set; }
        public FillValueWriteTime FillValueWriteTime { get; set; }
        public bool IsFillValueDefined { get; set; }
        public uint Size { get; set; }
        public byte[] FillValue { get; set; }

        #endregion
    }
}
