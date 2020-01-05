using System.IO;

namespace HDF5.NET
{
    public class SharedMessageTableMessage : Message
    {
        #region Constructors

        public SharedMessageTableMessage(BinaryReader reader) : base(reader)
        {
            //
        }

        #endregion

        #region Properties

        public byte Version { get; set; }
        public ulong SharedObjectHeaderMessageTableAddress { get; set; }
        public byte IndexCount { get; set; }

        #endregion
    }
}
