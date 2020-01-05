namespace HDF5.NET
{
    public class AttributeMessage
    {
#warning remember to parse different versions correctly
        #region Constructors

        public AttributeMessage()
        {
            //
        }

        #endregion

        #region Properties

        public uint Version { get; set; }
        public AttributeMessageFlags Flags { get; set; }
        public ushort NameSize { get; set; }
        public ushort DataTypeSize { get; set; }
        public ushort DataSpaceSize { get; set; }
        public CharacterSetEncoding NameCharacterSetEncoding { get; set; }
        public string Name { get; set; }
        public DatatypeMessage Datatype { get; set; }
        public DataspaceMessage Dataspace { get; set; }
        public byte[] Data { get; set; }

        #endregion
    }
}
