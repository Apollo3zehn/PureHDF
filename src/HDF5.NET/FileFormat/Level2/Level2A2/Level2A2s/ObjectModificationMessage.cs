using System.IO;

namespace HDF5.NET
{
    public class ObjectModificationMessage : Message
    {
        #region Constructors

        public ObjectModificationMessage(BinaryReader reader) : base(reader)
        {
            //
        }

        #endregion

        #region Properties

        public byte Version { get; set; }
        public uint SecondsAfterUnixEpoch { get; set; }

        #endregion
    }
}
