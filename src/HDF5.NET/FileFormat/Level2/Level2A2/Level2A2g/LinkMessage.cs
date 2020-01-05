namespace HDF5.NET
{
    public class LinkMessage
    {
        #region Constructors

        public LinkMessage()
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
