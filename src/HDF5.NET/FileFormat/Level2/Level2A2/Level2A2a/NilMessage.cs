using System.IO;

namespace HDF5.NET
{
    public class NilMessage : Message
    {
        #region Constructors

        public NilMessage(BinaryReader reader) : base(reader)
        {
            //
        }

        #endregion
    }
}
