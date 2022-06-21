namespace HDF5.NET
{
    public abstract partial class H5Object
    {
        #region Properties

        public string Name => Reference.Name;

        #endregion
    }
}
