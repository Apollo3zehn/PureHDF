using System.IO;

namespace HDF5.NET
{
    public abstract class SharedMessageRecord : FileBlock
    {
        #region Constructors

        public SharedMessageRecord(BinaryReader reader) : base(reader)
        {
            // message location
            this.MessageLocation = (MessageLocation)reader.ReadByte();
        }

        #endregion

        #region Properties

        public MessageLocation MessageLocation { get; set; }

        #endregion
    }
}
