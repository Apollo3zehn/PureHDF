namespace PureHDF
{
    internal class EnumerationPropertyDescription : DatatypePropertyDescription
    {
        #region Constructors

        internal EnumerationPropertyDescription(H5BaseReader reader, byte version, uint valueSize, ushort memberCount)
        {
            // base type
            BaseType = new DatatypeMessage(reader);

            // names
            Names = new List<string>(memberCount);

            for (int i = 0; i < memberCount; i++)
            {
                if (version <= 2)
                    Names.Add(H5ReadUtils.ReadNullTerminatedString(reader, pad: true));
                else
                    Names.Add(H5ReadUtils.ReadNullTerminatedString(reader, pad: false));
            }

            // values
            Values = new List<byte[]>(memberCount);

            for (int i = 0; i < memberCount; i++)
            {
                Values.Add(reader.ReadBytes((int)valueSize));
            }
        }

        #endregion

        #region Properties

        public DatatypeMessage BaseType { get; set; }
        public List<string> Names { get; set; }
        public List<byte[]> Values { get; set; }

        #endregion
    }
}