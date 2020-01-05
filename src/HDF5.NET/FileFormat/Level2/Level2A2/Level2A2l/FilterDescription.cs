namespace HDF5.NET
{
    public class FilterDescription
    {
#warning remember to parse different versions correctly
        #region Constructors

        public FilterDescription()
        {
            //
        }

        #endregion

        #region Properties

        public FilterIdentifier FilterIdentifier { get; set; }
        public ushort NameLength { get; set; }
        public FilterDescriptionFlags Flags { get; set; }
        public ushort ClientDataValueCount { get; set; }
        public string Name { get; set; }
        public byte[] ClientData { get; set; }

        #endregion
    }
}
