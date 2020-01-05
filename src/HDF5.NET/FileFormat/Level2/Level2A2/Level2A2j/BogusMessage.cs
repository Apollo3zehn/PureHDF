using System.IO;

namespace HDF5.NET
{
    public class BogusMessage : Message
    {
        #region Constructors

        public BogusMessage(BinaryReader reader) : base(reader)
        {
            //
        }

        #endregion

        #region Properties

        public uint BogusValue { get; set; }

        #endregion
    }
}
