namespace HDF5.NET
{
    internal struct NamedObject
    {
        #region Constructors

        public NamedObject(string name, ObjectHeader header)
        {
            this.Name = name;
            this.Header = header;
        }

        #endregion

        #region Properties

        public string Name { get; }
        public ObjectHeader Header { get; }

        #endregion
    }
}
