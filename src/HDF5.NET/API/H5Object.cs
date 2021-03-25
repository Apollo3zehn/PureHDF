namespace HDF5.NET
{
    public abstract partial class H5Object
    {
        #region Properties

        public string Name => this.Reference.Name;

        public uint ReferenceCount => this.GetReferenceCount();

        public H5NamedReference Reference { get; internal set; }

        #endregion
    }
}
