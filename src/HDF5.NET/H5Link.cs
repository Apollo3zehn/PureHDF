namespace HDF5.NET
{
    public abstract class H5Link
    {
        #region Constructors

        internal H5Link(string name)
        {
            this.Name = name;
        }

        #endregion

        #region Properties

        public string Name { get; }

        #endregion
    }
}
