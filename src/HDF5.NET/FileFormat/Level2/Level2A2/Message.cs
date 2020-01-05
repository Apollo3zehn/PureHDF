using System.IO;

namespace HDF5.NET
{
    public abstract class Message : FileBlock
    {
        #region Constructors

        public Message(BinaryReader reader) : base(reader)
        {
            //
        }

        #endregion
    }
}
