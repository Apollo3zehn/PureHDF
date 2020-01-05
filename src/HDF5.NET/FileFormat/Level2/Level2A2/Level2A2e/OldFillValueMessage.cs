using System.IO;

namespace HDF5.NET
{
    public class OldFillValueMessage : Message
    {
        #region Constructors

        public OldFillValueMessage(BinaryReader reader) : base(reader)
        {
            //
        }

        #endregion

        #region Properties

        public uint Size { get; set; }
        public byte[] FillValue { get; set; }

        #endregion
    }
}
