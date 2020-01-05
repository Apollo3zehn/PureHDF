using System.IO;

namespace HDF5.NET
{
    public class LinkMessage : Message
    {
        #region Constructors

        public LinkMessage(BinaryReader reader) : base(reader)
        {
            //
        }

        #endregion

        #region Properties

        public byte Version { get; set; }
        public byte Flags { get; set; }
        public LinkType LinkType { get; set; }
        public ulong CreationOrder { get; set; }
        public CharacterSetEncoding LinkNameCharacterSet { get; set; }
        public ulong LinkNameSize { get; set; }
        public string LinkName { get; set; }
        public byte[] LinkInformation { get; set; }

        #endregion
    }
}
